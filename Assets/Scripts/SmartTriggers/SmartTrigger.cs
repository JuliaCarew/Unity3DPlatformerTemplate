using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

// TODO: Make the dropdown for trigger options show the options with spaces between
// TODO: Add TriggerActions for Spawning with option of defining a spawn rotation/direction.
// TODO: On Interactable allow for adding a bool that will control whether pressing interact a second time will stop the interaction or do another action. (Possible with trigger action list)
// TODO: Separate the trigger action list from Smart Trigger so can reuse in interactables.
// TODO: Add option on Hazards to self-destruct after X amount of damages dealt.


/// <summary>
/// A flexible trigger system that can execute a sequence of actions based on various trigger conditions.
/// </summary>
[System.Flags]
public enum TriggerOptions
{
    TriggerWhenPlayerInteract = 1,
    TriggerWhenEnter = 2,       // Execute when objects enter the trigger
    TriggerWhenExit = 4,        // Execute when objects exit the trigger
    TriggerEveryFrameWhileInside = 8,     // Continuously execute while objects are inside
    UntriggerOtherwise = 16,     // Execute untrigger actions when no valid objects are inside
    TriggerOnSpawn = 32,
    TriggerOnCollide = 64,  // Execute on collider enter
    HasCooldown = 128,
    RequiresMinWeight = 256,
    TriggerOnlyOnce = 1024,        // The trigger will only execute its actions once
    UntriggeringInterruptsExecution = 2056,
}

/// <summary>
/// Main component for the Smart Trigger system. Handles collision detection and orchestrates the execution of trigger actions.
/// Place this component on a GameObject with a Collider (set to "Is Trigger").
/// </summary>
public class SmartTrigger : MonoBehaviour
{
    [Tooltip("Configure how and when the trigger should activate")]
    [SerializeField] private TriggerOptions triggerOptions;

    [Tooltip("Which layers can activate this trigger")]
    [SerializeField] private LayerMask triggerLayers;

    [Tooltip("Cooldown between activations of this trigger")]
    [SerializeField] private float cooldownBeforeReactivation = 0.0f;

    [Tooltip("Weight required to activate this trigger")]
    [SerializeField] private float requiredWeight = 0.0f;

    [Tooltip("Weight required to activate this trigger")]
    [SerializeField] private bool togglableByInteraction = false;

    [Tooltip("Which tags can activate this trigger (leave empty to accept any tag)")]
    [SerializeField][TagDropdown] private List<string> triggerTags = new List<string>();

    [Tooltip("Actions to execute when the trigger activates")]
    [SerializeReference] public TriggerActionsList onTriggerActions = new TriggerActionsList();

    [Tooltip("Actions to execute when the trigger deactivates (only used with UntriggerOtherwise option)")]
    [SerializeReference] public TriggerActionsList onUntriggerActions = new TriggerActionsList();

    private bool hasAlreadyTriggered => onTriggerActions.hasAlreadyExecuted;
    private HashSet<Collider> triggeredColliders = new HashSet<Collider>();
    private List<Rigidbody> triggeredRigidbodies = new List<Rigidbody>();

    private bool isInCooldown = false;
    private bool isInTriggeredState = false;
    private float currentWeight = 0f;

    private TriggerInterruptorHolder untriggerInterruptor = new TriggerInterruptorHolder();
    private TriggerInterruptorHolder triggerInterruptor = new TriggerInterruptorHolder();

    protected bool hasAnyTriggerActions { get => onTriggerActions.Count > 0; }
    protected bool hasAnyUntriggerActions { get => onUntriggerActions.Count > 0; }
    private InteractionController lastInteractionController;
    private PlayerController lastPlayerTriggered;
    public PlayerController FindLastPlayerInteractor() {
        if (lastInteractionController != null)
            return lastInteractionController.GetComponentInParent<PlayerController>();
        if (lastPlayerTriggered != null) return lastPlayerTriggered;
        return null;
    }
    SmartTriggerInteractHelper interactHelper = null;
    public void Awake()
    {
        if (triggerOptions.HasFlag(TriggerOptions.TriggerWhenPlayerInteract))
        {
            TryGetComponent<SmartTriggerInteractHelper>(out interactHelper);
            if (interactHelper == null)
                interactHelper = gameObject.AddComponent<SmartTriggerInteractHelper>();
            interactHelper.SetSmartTrigger(this);
        }
    }

    public void Start() {
        if (triggerOptions.HasFlag(TriggerOptions.TriggerOnSpawn)) {
            ExecuteTriggerActions();
        }
    }

    public bool TriggeringFromInteractHelper(InteractionController controller)
    {
        bool result = false;
        bool doingTrigger = true; ;
        if (togglableByInteraction)
        {
            lastInteractionController = controller;
            if (isExecutingTriggers || isInTriggeredState) {
                result = true;
                doingTrigger = false;
                ExecuteUntriggerActions();
            } else
                result = ProcessTriggerEnter(controller.GetComponent<Collider>(), true);
        } else
            result = ProcessTriggerEnter(controller.GetComponent<Collider>(), true);

        if (result) {
            if (!togglableByInteraction)
            {
                StartCoroutine(RemoveInteractionControllerOnInteractComplete(controller, doingTrigger));
            } else
            {
                interactHelper.OnInteractionEnd(controller);
                controller.EndCurrentInteraction();
            }
        }
        return result;
    }

    public IEnumerator RemoveInteractionControllerOnInteractComplete(InteractionController controller, bool Triggering)
    {
        yield return null;
        while (isExecutingTriggers) {
            yield return null;
        }
        interactHelper.OnInteractionEnd(controller);
        if (controller.IsCurrentInteractingWithThis(interactHelper)) controller.EndCurrentInteraction();
        ProcessTriggerExit(controller.GetComponent<Collider>(), true);

        Debug.Log($"Ending interaction");
    }

    public int GetTriggerListCount()
    {
        return onTriggerActions.Count;
    }
    public void SetTriggerListElement(int indx, TriggerAction newAction)
    {
        if (indx >= onTriggerActions.Count)
        {
            onTriggerActions.Add(newAction);
        }
        onTriggerActions[indx] = newAction;
    }

    public void SetUnTriggerListElement(int indx, TriggerAction newAction)
    {
        if (indx >= onUntriggerActions.Count)
        {
            onUntriggerActions.Add(newAction);
        }
        onUntriggerActions[indx] = newAction;
    }

    public TriggerAction GetTriggerListElement(int indx)
    {
        return onTriggerActions[indx];
    }

    private void OnCollisionEnter(Collision other) {
        if (triggerOptions.HasFlag(TriggerOptions.TriggerOnCollide))
            ProcessTriggerEnter(other.collider);
    }

    private void OnTriggerEnter(Collider other) {
        ProcessTriggerEnter(other);
    }

    private bool ProcessTriggerEnter(Collider other, bool ComingFromPlayerInteract = false)
    {
        if (other.isTrigger) return false;
        if (!IsValidTrigger(other)) return false;
        //Debug.Log($"Processing collider {other} {other.gameObject} has player controller? {other.GetComponent<PlayerController>() != null}", other);
        triggeredColliders.Add(other);

        // Track rigidbody for weight calculation
        if (other.attachedRigidbody != null && !triggeredRigidbodies.Contains(other.attachedRigidbody))
        {
            triggeredRigidbodies.Add(other.attachedRigidbody);
            UpdateTotalWeight();
        }

        if (triggerOptions.HasFlag(TriggerOptions.TriggerWhenEnter) || (triggerOptions.HasFlag(TriggerOptions.TriggerWhenPlayerInteract) && ComingFromPlayerInteract))
        {
            if (triggerOptions.HasFlag(TriggerOptions.TriggerOnlyOnce) && hasAlreadyTriggered) {
                return false;
            }
            if (!CanTrigger()) return false;
            
            return ExecuteTriggerActions();
        }
        return false;
    }

    private void OnTriggerExit(Collider other)
    {
        ProcessTriggerExit(other);
    }

    private void ProcessTriggerExit(Collider other, bool ComingFromPlayerInteract = false)
    {
        if (other.isTrigger) return;
        if (!IsValidTrigger(other)) return;

        triggeredColliders.Remove(other);

        // Remove rigidbody and update weight
        if (other.attachedRigidbody != null)
        {
            triggeredRigidbodies.Remove(other.attachedRigidbody);
            UpdateTotalWeight();
        }

        if (triggerOptions.HasFlag(TriggerOptions.TriggerWhenExit) && !ComingFromPlayerInteract)
        {
            if (triggerOptions.HasFlag(TriggerOptions.TriggerOnlyOnce) && hasAlreadyTriggered) return;
            ExecuteTriggerActions();
        }

        if (triggeredColliders.Count == 0 && isInTriggeredState)
        {
            if (triggerOptions.HasFlag(TriggerOptions.UntriggerOtherwise))
            {
                ExecuteUntriggerActions();
            } else
            {
                if (!ComingFromPlayerInteract || !togglableByInteraction) {
                    isInTriggeredState = false;
                }
            }
        }
    }

    private void Update()
    {
        if (triggerOptions.HasFlag(TriggerOptions.TriggerEveryFrameWhileInside) && triggeredColliders.Count > 0)
        {
            if (triggerOptions.HasFlag(TriggerOptions.TriggerOnlyOnce) && hasAlreadyTriggered) return;
            if (!CanTrigger()) return;
            ExecuteTriggerActions();
        }
    }

    private void UpdateTotalWeight()
    {
        currentWeight = triggeredRigidbodies.Sum(rb => rb.mass);
    }

    public bool CanTrigger()
    {
        // Check cooldown
        if (triggerOptions.HasFlag(TriggerOptions.HasCooldown) && isInCooldown)
        {
            return false;
        }

        // Check weight requirement
        if (triggerOptions.HasFlag(TriggerOptions.RequiresMinWeight) && currentWeight < requiredWeight)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a collider meets the layer and tag requirements to activate this trigger.
    /// </summary>
    public bool IsValidTrigger(Collider other)
    {
        // Check layer
        if (!(triggerLayers.isLayerInLayerMask(other.gameObject.layer)))
            return false;

        // Check tags
        if (triggerTags != null && triggerTags.Count > 0)
        {
            bool hasValidTag = false;
            foreach (string tag in triggerTags)
            {
                if (other.CompareTag(tag))
                {
                    hasValidTag = true;
                    break;
                }
            }
            if (!hasValidTag) return false;
        }

        return true;
    }

    public bool IsExecuting { get => isExecutingTriggers || isExecutingUntriggers; }
    

    bool isExecutingTriggers => onTriggerActions.isExecutingList;
    bool isExecutingUntriggers => onUntriggerActions.isExecutingList;
    protected bool ExecuteTriggerActions()
    {
        if (isExecutingTriggers || isInTriggeredState)
            return false;

        if (isExecutingUntriggers && triggerOptions.HasFlag(TriggerOptions.UntriggeringInterruptsExecution)) {
            onUntriggerActions.Interrupt();
        }

        onTriggerActions.ClearInterruption();
        
        foreach (var item in triggeredColliders)
            if (item.TryGetComponent<PlayerController>(out PlayerController playerController)) {
                    lastPlayerTriggered = playerController;
                }

        if (onTriggerActions.ExecuteTriggerActions(this, () => { onTriggerActions.ClearInterruption(); }, triggerOptions.HasFlag(TriggerOptions.UntriggeringInterruptsExecution)))
        {
            isInTriggeredState = true;

            // Start cooldown if enabled
            if (triggerOptions.HasFlag(TriggerOptions.HasCooldown)) {
                StartCoroutine(CooldownRoutine());
            }
            return true;
        }

        return false;
    }

    protected bool ExecuteUntriggerActions()
    {
        if (isExecutingUntriggers)
            return false;

        isInTriggeredState = false;
        if (isExecutingTriggers && triggerOptions.HasFlag(TriggerOptions.UntriggeringInterruptsExecution))
            onTriggerActions.Interrupt();

        onUntriggerActions.ClearInterruption();

        if (onUntriggerActions.ExecuteTriggerActions(this, () =>
        {
            isInTriggeredState = false;
            onUntriggerActions.ClearInterruption();
        }, triggerOptions.HasFlag(TriggerOptions.UntriggeringInterruptsExecution)))
        {
            isInTriggeredState = false;

            // Start cooldown if enabled
            if (triggerOptions.HasFlag(TriggerOptions.HasCooldown))
            {
                StartCoroutine(CooldownRoutine());
            }
            return true;
        }
        return false;
    }

    private IEnumerator CooldownRoutine()
    {
        isInCooldown = true;
        yield return new WaitForSeconds(cooldownBeforeReactivation);
        isInCooldown = false;
    }
}
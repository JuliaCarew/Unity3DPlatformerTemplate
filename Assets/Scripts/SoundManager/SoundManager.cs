//SoundManager by Daniel Snd (http://snddev.tumblr.com/utilities) is licensed under:
/* The MIT License (MIT)
Copyright (c) 2013 UnityPatterns
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the �Software�), to deal in the
Software without restriction, including without limitation the rights to use, copy,
modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the
following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. */

using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Soundmanager helps
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    public Dictionary<string, SoundGroup> SoundGroupsDictionary;

    Dictionary<SoundGroup, List<SoundGroup>> pooledObjects = new Dictionary<SoundGroup, List<SoundGroup>>();
    Dictionary<SoundGroup, SoundGroup> spawnedObjects = new Dictionary<SoundGroup, SoundGroup>();

    public SoundGroup currentMusic;

    public bool ShowLogs = false;

    public float SFXVolume = 1;
    public float MusicVolume = 1;
   

    private Transform SoundPool, SoundPlaying;
    

    protected override void Awake()
    {
        base.Awake();

        if (creationFailed)
            return;

        gameObject.AddComponent<AudioListener>();

        //Time to create the sound group dictionary to be able to play soundgroups using strings with their names.
        CreateSoundGroupDictionary();
        
        //Create the containers for the pooling system
        CreatePoolContainers();
        SceneManager.sceneLoaded += SceneWasLoaded;
        //Since OnLevelWasLoaded doesn't get called on first awake, let's call it ourselves :)
        SceneWasLoaded();
    }

    public void OnDestroy() {
        if (this == _instance)
            SceneManager.sceneLoaded -= SceneWasLoaded;
    }

    private void SceneWasLoaded(Scene arg0, LoadSceneMode arg1)
    {
        SceneWasLoaded();
    }


    /// <summary>
    /// This method creates the containers for the pooling system to use.
    /// </summary>
    private void CreatePoolContainers()
    {
        SoundPool = new GameObject().transform;
        SoundPool.name = "SoundPool";
        SoundPool.SetParent(transform);
        SoundPlaying = new GameObject().transform;
        SoundPlaying.name = "SoundPlaying";
        SoundPlaying.SetParent(transform);
    }

    /// <summary>
    /// This method looks under the SoundGroups folder on the Resources folder and
    /// loads all of the found SoundGroups into a dictionary, saving their names.
    /// This way we can play sounds by calling for a string with their name.
    /// </summary>
    private void CreateSoundGroupDictionary()
    {
        //Load all sounds from the SoundGroups folder in resources
        SoundGroup[] SoundGroups = Resources.LoadAll<SoundGroup>("SoundGroups");

        //Create a new dictionary to store those values with a string key.
        SoundGroupsDictionary = new Dictionary<string, SoundGroup>();

        //For each found SoundGroup
        for (int i = 0; i < SoundGroups.Length; i++)
        {
            //See if there is already an object with that name on the dictionary
            SoundGroup Snd;
            if (SoundGroupsDictionary.TryGetValue(SoundGroups[i].gameObject.name, out Snd))
            {
                if (ShowLogs) Debug.Log("Soundgroup already exists " + SoundGroups[i].gameObject.name);
            }
            else
            {
                //If there isn't anything in the dictionary with this name yet, add it.
                SoundGroupsDictionary.Add(SoundGroups[i].gameObject.name, SoundGroups[i]);
                //Also add to the pool so we can spawn it from there later.
                AddToPool(SoundGroups[i]);
            }
        }
    }

    //This function is called by unity when a new level is loaded.
    void SceneWasLoaded()
    {
        //Update the Volumes based on playerprefs.
        PickPlayerPrefsVolume();

        //Attempt to play a soundgroup with the current scene name if there is one available.
        Play(SceneManager.GetActiveScene().name);

        //Attempt to transition to a snapshot with the current scene name if there is one available.
        //if(UseAudioMixer)
            //ExecuteSnapshotTransition(Application.loadedLevelName, 1);
    }

    /// <summary>
    /// It updates the variables MusicVolume and SFXVolume based on an int
    /// value on the player prefs between 0 and 10 (10 being 100% and 0 being 0%)
    /// </summary>
    public void PickPlayerPrefsVolume()
    {
        //Set Volume variables based on PlayerPrefs value
        MusicVolume = (PlayerPrefs.GetInt("musicvolume", 10)*1f)/10f;
        SFXVolume = (PlayerPrefs.GetInt("sfxvolume", 10)*1f)/10f;
        //For each sound group currently spawned in the game, tell it to update volume based on these values.
        foreach (SoundGroup _soundGroup in spawnedObjects.Keys)
        {
            _soundGroup.SetVolume();
        }
    }

    /// <summary>
    /// Play soundgroup using a string with the soundgroup name
    /// </summary>
    /// <param name="sndName">Desired SoundGroup name</param>
    /// <param name="pos">Desired position to spawn the sound in</param>
    /// <returns></returns>
    public static SoundGroup Play(string sndName, Vector3 pos)
    {
        //If we called this with an empty string, do nothing.
        if (String.IsNullOrEmpty(sndName))
            return null;

        //Try to get the prefab from the string.
        SoundGroup SndToPlay = null;
        if (SoundManager.Instance.SoundGroupsDictionary.TryGetValue(sndName, out SndToPlay))
        {
            //If we found the prefab we want but it's a music and we're already playing it, return that music.
            if (SndToPlay.Music && Instance.currentMusic && Instance.currentMusic.identifier == sndName)
                 return Instance.currentMusic;
            //If we found the prefab and we should play it, call the Play method that uses the prefab.
            return Play(SndToPlay,pos);
        }
        else
        {
            //We didn't find the soundgroup :( can't spawn stuff.
            if (Instance.ShowLogs) Debug.Log("[SoundManager] Attempted to play SoundGroup " + sndName + " but it was not found in the dictionary");
        }

        //If we got to this point on the script, return nothing.
        return null;
    }
    /// <summary>
    /// Play the SoundGroup prefab in specific position
    /// </summary>
    /// <param name="sndGroup">SoundGroup Prefab</param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static SoundGroup Play(AudioClip audioClip, Vector3 pos)
    {
        //If there is no sndGroup Prefab return null;
        if (audioClip == null) return null;
        if (!SoundManager.Instance.SoundGroupsDictionary.TryGetValue(audioClip.name, out SoundGroup soundGroup)) {
            CreateNewSoundGroupFromAudioClip(audioClip);
        }
        return Play(audioClip.name, pos);
    }

    private static void CreateNewSoundGroupFromAudioClip(AudioClip audioClip)
    {
        var newSoundGroupGO = new GameObject(audioClip.name);
        var newAudioSource = newSoundGroupGO.AddComponent<AudioSource>();
        newAudioSource.clip = audioClip;
        newSoundGroupGO.SetActive(false);
        var newSoundGroup = newSoundGroupGO.AddComponent<SoundGroup>();
        newSoundGroup.RandomPitch = false;
        newSoundGroup.Sounds = new AudioClip[] { audioClip };
        newAudioSource.clip = audioClip;
        newSoundGroupGO.SetActive(true);
        //Debug.Log("Create new sound group from audio clip");
        SoundManager.Instance.pooledObjects.Add(newSoundGroup, new List<SoundGroup>() { newSoundGroup });
        SoundManager.Instance.spawnedObjects[newSoundGroup] = newSoundGroup;
        SoundManager.Instance.SoundGroupsDictionary[audioClip.name] = newSoundGroup;
        RecycleSoundToPool(newSoundGroup);
    }

    /// <summary>
    /// Play the SoundGroup prefab in specific position
    /// </summary>
    /// <param name="sndGroup">SoundGroup Prefab</param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static SoundGroup Play(SoundGroup sndGroup, Vector3 pos)
    {
        //If there is no sndGroup Prefab return null;
        if (sndGroup == null) return null;

        //Attempt to spawn the soundgroup from the pool at the position and set it to the local variable thisSound
        SoundGroup thisSound = SpawnSoundGroup(sndGroup, pos);

        //If something goes wrong and we don't spawn anything
        if (thisSound == null) return null;

        //If the sound we spawned is a music
        if (thisSound.Music)
        {
            //Stops and recycles the music that was currently playing if there is one.
            if (Instance.currentMusic) Instance.currentMusic.ForceStop();

            //Set the current music to be our newly spawned music
            Instance.currentMusic = thisSound;
            //Name it appropriately
            thisSound.name = "_Music_" + sndGroup.name;
        }
        else
        {
            //Name it appropriately
            thisSound.name = "_SFX_" + sndGroup.name;
        }

        //Set the soundgroup identifier to be the prefab name.
        thisSound.identifier = sndGroup.name;

        //Return our newly spawned soundgroup.
        return thisSound;
    }

    #region OptionalPlayMethods
    /// <summary>
    /// Play soundgroup with it's string name at position 0
    /// </summary>
    /// <param name="sndName">Desired SoundGroup name to play</param>
    /// <returns></returns>
    public static SoundGroup Play(string sndName)
    {
        return Play(sndName, Vector3.zero);
    }

    /// <summary>
    /// Play the SoundGroup prefab at position 0
    /// </summary>
    /// <param name="sndGroup">SoundGroup Prefab</param>
    /// <returns></returns>
    public static SoundGroup Play(SoundGroup sndGroup)
    {
        return Play(sndGroup, Vector3.zero);
    }

    /// <summary>
    /// Play music from string name.
    /// </summary>
    /// <param name="sndName"></param>
    /// <returns></returns>
    public static SoundGroup PlayMusic(string sndName)
    {
        if (Instance.ShowLogs) Debug.Log("Play music " + sndName);

        if (Instance.currentMusic)
        {
            if (Instance.currentMusic.identifier == sndName)
                return Instance.currentMusic;

            Instance.currentMusic.ForceStop();
        }

        return Play(sndName, Vector3.zero);
    }

    #endregion

    /// <summary>
    /// Call this on something's start method if you want to initialize the SoundManager before playing a sound from script
    /// Useful if you're playing music automatically with the level name.
    /// </summary>
    public void Initialize()
    {
        if (Instance.ShowLogs) Debug.Log("SoundManager Initialized");
    }

    /// <summary>
    /// Call this on something's start method if you want to initialize the SoundManager before playing a sound from script
    /// Useful if you're playing music automatically with the level name.
    /// </summary>
    public static void Init()
    {
        Instance.Initialize();
    }

    #region PoolingSystem
    /// <summary>
    /// Adds this Soundgroup prefab to our pool of spawnable prefabs
    /// </summary>
    /// <param name="prefab">Soundgroup prefab to add</param>
    public static void AddToPool(SoundGroup prefab)
    {
        if (IsQuittingGame) return;
        // If the prefab isn't null and it doesn't exist on our dictionary yet, add it.
        if (prefab != null && !Instance.pooledObjects.ContainsKey(prefab))
        {
            var list = new List<SoundGroup>();
            Instance.pooledObjects.Add(prefab, list);
        }
    }

    /// <summary>
    /// Spawn soundgroup from pool
    /// </summary>
    /// <param name="prefab">Desired Prefab to spawn</param>
    /// <param name="position">Desired spawn position</param>
    /// <returns></returns>
    public static SoundGroup SpawnSoundGroup(SoundGroup prefab, Vector3 position)
    {
        if (IsQuittingGame) return null;
        List<SoundGroup> list;
        SoundGroup obj;
        obj = null;
        //Get a list from the dictionary with the current objects of this prefab we have on the pool
        if (Instance.pooledObjects.TryGetValue(prefab, out list))
        {
            //While we don't have an object to spawn and our list still has objects in there.
            while (obj == null && list.Count > 0)
            {
                //While we haven't picked one, check if the one at 0 is one we can use.
                if (list[0] != null)
                    obj = list[0];
                //Remove the one at 0.
                list.RemoveAt(0);
            }
        }
        else
        {
            //This prefab is definitely not in the list D: let's add it there for later.
            AddToPool(prefab);
        }

        //If I still don't have an object to spawn, means my pool doesn't have that object, let's instantiate one old-style.
        if (obj == null) obj = (SoundGroup)Instantiate(prefab);

        //Set the object's name to be my prefab name.
        obj.transform.name = prefab.name;

        //Parent it to the Current Playing Transform
        obj.transform.SetParent(Instance.SoundPlaying);

        //Set the position to be the desired position.
        obj.transform.position = position;

        //Set it's gameobject to active (if it's coming from the pool it was deactivated)
        obj.gameObject.SetActive(true);

        //Add it to the list of currently spawned objects
        Instance.spawnedObjects.Add(obj, prefab);

        //Return the spawned object.
        return obj;
    }

    /// <summary>
    /// Send the soundgroup object back to the pool to be reused later.
    /// </summary>
    /// <param name="obj">Soundgroup object to recycle</param>
    public static void RecycleSoundToPool(SoundGroup obj)
    {
        //Debug.Log("Recycle to pool");
        
        if (IsQuittingGame || obj == null) return;

        //Try and get the prefab reference from the pool dictionary
        SoundGroup groupPrefab = null;
        Instance.spawnedObjects.TryGetValue(obj, out groupPrefab);

        //If the prefab couldn't be found
        if (groupPrefab == null)
        {
            //Destroy the object oldschool way, it wasn't pooled :(
            Destroy(obj.gameObject);
            return;
        }

        //If the object isn't null
        if (obj != null)
        {
            //Add it back to the pool list
            Instance.pooledObjects[groupPrefab].Add(obj);
            //Remove it from the currently spawned objects list
            Instance.spawnedObjects.Remove(obj);
        }

        //Parent the object back to our pool container and hide it
        obj.transform.SetParent(Instance.SoundPool);
        obj.gameObject.SetActive(false);
    }
    #endregion
}

public static class SoundManagerExtensionMethods
{
    /// <summary>
    /// Play soundgroup from string name
    /// Example usage: "MyCoolSound".PlaySound();
    /// </summary>
    /// <param name="snd">Desired soundgroup name</param>
    public static void PlaySound(this string snd)
    {
        if(!String.IsNullOrEmpty(snd))
            SoundManager.Play(snd);
    }

    /// <summary>
    /// Play soundgroup from string name at a specific position
    /// Example usage: "MyCoolSound".PlaySound(new Vector3(0,3,2));
    /// </summary>
    /// <param name="snd">Desired soundgroup name</param>
    /// <param name="pos">Desired position to spawn at</param>
    public static void PlaySound(this string snd,Vector3 pos)
    {
        if (!String.IsNullOrEmpty(snd))
            SoundManager.Play(snd,pos);
    }
    /// <summary>
    /// Play soundgroup from string name
    /// Example usage: "MyCoolSound".PlaySound();
    /// </summary>
    /// <param name="snd">Desired audio clip</param>
    public static void PlaySound(this AudioClip snd, Vector3 pos)
    {
        if (snd == null) return;
        SoundManager.Play(snd, pos);
    }

    /// <summary>
    /// Play music soundgroup from string name
    /// Example usage: "MyCoolSound".PlaySound();
    /// </summary>
    /// <param name="snd">Desired soundgroup name</param>
    public static void PlayMusic(this string snd)
    {
        if (!String.IsNullOrEmpty(snd))
            SoundManager.PlayMusic(snd);
    }
}

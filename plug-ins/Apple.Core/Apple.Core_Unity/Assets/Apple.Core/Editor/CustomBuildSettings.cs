using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apple.Core
{
    [Serializable]
    public class CustomBuildSettings
    {
        [Tooltip("If enabled, automatic set visionOS platform in XCode instead of xrsimulator")]
        public bool setPlatformVisionOsOnSimulator = false;
        
        [Header("Custom Keys Info.plist")]
        [Tooltip("If enabled, automatic add the next keys to info.plist file of xcode project")]
        public bool addCustomKeysInfoPlist = true;
        public List<CustomStringKeyInfoPlist> customStringKeysInfoPlist = new List<CustomStringKeyInfoPlist>();
        public List<CustomBoolKeyInfoPlist> customBoolKeysInfoPlist = new List<CustomBoolKeyInfoPlist>();
        [Header("Custom Build")]
        public List<CustomStringKeyInfoPlist> customBuildProperties = new List<CustomStringKeyInfoPlist>();

        [Header("Custom Fixes")]
        public bool visionOsAudioFix = false;
        
        [Header("AppIcon")]
        public bool addAppIcon = true;
        public Texture2D appIconFront;
        public Texture2D appIconMiddle;
        public Texture2D appIconBack;
        
        public bool HasIcons => appIconFront != null && appIconMiddle != null && appIconBack != null;
        
        public abstract class CustomKeyInfoPlist<T>
        {
            public string key;
            public T value;
            public bool enabled = true;
            
            public bool IsEnabled => enabled && !string.IsNullOrEmpty(key);
        }
        
        [Serializable]
        public class CustomStringKeyInfoPlist : CustomKeyInfoPlist<string>
        {
        }
        
        [Serializable]
        public class CustomBoolKeyInfoPlist : CustomKeyInfoPlist<bool>
        {
        }

    }
}
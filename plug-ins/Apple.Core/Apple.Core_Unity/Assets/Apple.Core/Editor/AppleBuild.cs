#if UNITY_EDITOR_OSX && (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_STANDALONE_OSX)
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Apple.Core
{
    public static class AppleBuild
    {
        /// <summary>
        /// Executes all post-build AppleBuildSteps as structured in the AppleBuildProfile.
        /// </summary>
        [PostProcessBuild(10)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            // TODO: Add management for multiple build profiles.
            var appleBuildProfile = AppleBuildProfile.DefaultProfile();
            var customSettings = appleBuildProfile.customBuildSettings;

            // Ensure if we are building an app, that it includes .app at the end...
            if (buildTarget == BuildTarget.StandaloneOSX && !IsXcodeGeneratedMac() && !pathToBuiltProject.EndsWith(".app"))
            {
                pathToBuiltProject += ".app";
            }

            #region Begin Post Process

            Debug.Log($"AppleBuild: OnBeginPostProcess begin");
            Debug.Log($"AppleBuild: Found {appleBuildProfile.buildSteps.Count} build steps.");
            Debug.Log($"AppleBuild: Outputting to project at path {pathToBuiltProject}.");

            foreach (var buildStep in appleBuildProfile.buildSteps)
            {
                if (buildStep.Value.IsEnabled)
                {
                    Debug.Log($"AppleBuild: OnBeginPostProcess for step: {buildStep.Key}");
                    buildStep.Value.OnBeginPostProcess(appleBuildProfile, buildTarget, pathToBuiltProject);
                }
                else
                {
                    Debug.Log($"AppleBuild: Build post process disabled for build step: {buildStep.Key}");
                }
            }

            Debug.Log($"AppleBuild: OnBeginPostProcess end.");

            #endregion // Begin Post Process

            #region Process info.plist

            if (appleBuildProfile.AutomateInfoPlist)
            {
                Debug.Log($"AppleBuild: OnProcessInfoPlist begin...");

                var infoPlist = new PlistDocument();
                var infoPlistPath = GetInfoPlistPath(buildTarget, pathToBuiltProject);
                infoPlist.ReadFromFile(infoPlistPath);

                // Required property which notifies Apple about 3rd party encryption...
                infoPlist.root.SetBoolean("ITSAppUsesNonExemptEncryption", appleBuildProfile.AppUsesNonExemptEncryption);

                string minOSVersionString = string.Empty;
                switch (buildTarget)
                {
                    case BuildTarget.iOS:
                        minOSVersionString = appleBuildProfile.MinimumOSVersion_iOS;
                        break;

                    case BuildTarget.tvOS:
                        minOSVersionString = appleBuildProfile.MinimumOSVersion_tvOS;
                        break;

                    case BuildTarget.StandaloneOSX:
                        minOSVersionString = appleBuildProfile.MinimumOSVersion_macOS;
                        break;
                    
                    case BuildTarget.VisionOS:
                        minOSVersionString = appleBuildProfile.MinimumOSVersion_visionOS;
                        break;

                    default:
                        break;
                }

                if (!string.IsNullOrEmpty(minOSVersionString))
                {
                    infoPlist.root.SetString("LSMinimumSystemVersion", minOSVersionString);
                }

                // Ensure we update keys with our default plist...
                if (appleBuildProfile.DefaultInfoPlist != null)
                {
                    var defaultPlist = new PlistDocument();
                    defaultPlist.ReadFromFile(AssetDatabase.GetAssetPath(appleBuildProfile.DefaultInfoPlist));

                    foreach (var pair in defaultPlist.root.values)
                    {
                        Debug.Log($"AppleBuild: Setting InfoPlist [{pair.Key}] to [{pair.Value}].");
                        infoPlist.root[pair.Key] = pair.Value;
                    }
                }

                foreach (var buildStep in appleBuildProfile.buildSteps)
                {
                    if (buildStep.Value.IsEnabled)
                    {
                        Debug.Log($"AppleBuild: OnProcessInfoPlist for step: {buildStep.Key}");
                        buildStep.Value.OnProcessInfoPlist(appleBuildProfile, buildTarget, pathToBuiltProject, infoPlist);
                    }
                }
                
                //!Hack: Kluge Fix to autoadd some XCode project properties automatically for VisionOS
                // infoPlist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
                // infoPlist.root.SetBoolean("GCSupportsControllerUserInteraction", false);
                // infoPlist.root.SetBoolean("UIApplicationExitsOnSuspend", true);
                // infoPlist.root.SetBoolean("NSApplicationRequiresArcade", true);
                // infoPlist.root.SetString("NSLocalNetworkUsageDescription", "For access to Game Center and Leaderboards");
                // infoPlist.root.SetString("NSGKFriendListUsageDescription", "To display Friends on your Game Center Leaderboards");

                if (customSettings is { addCustomKeysInfoPlist: true })
                {
                    foreach(var customStringKey in customSettings.customStringKeysInfoPlist)
                        if(customStringKey.IsEnabled)
                            infoPlist.root.SetString(customStringKey.key, customStringKey.value);
                    
                    foreach(var customBoolKey in customSettings.customBoolKeysInfoPlist)
                        if(customBoolKey.IsEnabled)
                            infoPlist.root.SetBoolean(customBoolKey.key, customBoolKey.value);
                }


                infoPlist.WriteToFile(infoPlistPath);

                Debug.Log($"AppleBuild: OnProcessInfoPlist end...");
            }

            #endregion // Process info.plist

            #region Process Entitlements

            var pbxProject = GetPbxProject(buildTarget, pathToBuiltProject);
            var pbxProjectPath = GetPbxProjectPath(buildTarget, pathToBuiltProject);
            var targetGuid = buildTarget == BuildTarget.StandaloneOSX ? pbxProject.TargetGuidByName(Application.productName) : pbxProject.GetUnityMainTargetGuid();

            if (appleBuildProfile.AutomateEntitlements)
            {
                Debug.Log($"AppleBuild: OnProcessEntitlements begin...");

                var entitlementsPath = GetEntitlementsPath(buildTarget, pathToBuiltProject);
                var entitlements = new PlistDocument();

                if (File.Exists(entitlementsPath))
                {
                    entitlements.ReadFromFile(entitlementsPath);
                }
                else
                {
                    entitlements.Create();
                }

                // Ensure we update keys with our default entitlements
                if (appleBuildProfile.DefaultEntitlements != null)
                {
                    var defaultPlist = new PlistDocument();
                    defaultPlist.ReadFromFile(AssetDatabase.GetAssetPath(appleBuildProfile.DefaultEntitlements));

                    foreach (var pair in defaultPlist.root.values)
                    {
                        Debug.Log($"AppleBuild: Setting Entitlements [{pair.Key}] to [{pair.Value}].");
                        entitlements.root[pair.Key] = pair.Value;
                    }
                }

                // Set application id if macOS
                var applicationIdentifier = PlayerSettings.GetApplicationIdentifier(EditorUserBuildSettings.selectedBuildTargetGroup);
                if (buildTarget == BuildTarget.StandaloneOSX)
                {
                    entitlements.root.SetString("com.apple.application-identifier", $"{PlayerSettings.iOS.appleDeveloperTeamID}.{applicationIdentifier}");
                }

                foreach (var buildStep in appleBuildProfile.buildSteps)
                {
                    if (buildStep.Value.IsEnabled)
                    {
                        Debug.Log($"AppleBuild: OnProcessEntitlements for step: {buildStep.Key}");
                        buildStep.Value.OnProcessEntitlements(appleBuildProfile, buildTarget, pathToBuiltProject, entitlements);
                    }
                }

                entitlements.WriteToFile(entitlementsPath);

                FixEntitlementFormatting(entitlementsPath);

                // Add CODE_SIGN_ENTITLEMENTS to pbxProject
                if (pbxProject != null)
                {
                    var entitlementsXCodePath = buildTarget == BuildTarget.StandaloneOSX ? $"{Application.productName}/{Application.productName}.entitlements" : $"{Application.productName}.entitlements";
                    pbxProject.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsXCodePath);

                    Debug.Log($"AppleBuild: Writing changes to PBXProject {pbxProjectPath}...");
                    pbxProject.WriteToFile(pbxProjectPath);
                }

                Debug.Log($"AppleBuild: OnProcessEntitlements end.");
            }

            #endregion // Process Entitlements

            //!Hack: Kluge Fix for VisionOS (in order to automatic set visionOS platform in XCode instead of xrsimulator)
            if (buildTarget == BuildTarget.VisionOS &&
                pbxProject != null &&
                PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Simulator &&
                appleBuildProfile.customBuildSettings is { setPlatformVisionOsOnSimulator: true })
            {
                pbxProject.SetBuildProperty(targetGuid, "SUPPORTED_PLATFORMS", "xrsimulator xros");
                // pbxProject.SetBuildProperty(targetGuid,"SUPPORTED_PLATFORMS","xrsimulator xros visionOS");
            }
            
            //Add VisionOS AppIcon
            if (customSettings is { addAppIcon: true, HasIcons: true })
            {
                var iconGuidFront = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(customSettings.appIconFront));
                var iconGuidMiddle = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(customSettings.appIconMiddle));
                var iconGuidBack = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(customSettings.appIconBack));
                
                XcodePostProcessUtils.SetupVisionOSIcon(
                    buildPath: pathToBuiltProject,
                    version: 1,
                    author: "Xcode",
                    Tuple.Create("Front", iconGuidFront),
                    Tuple.Create("Middle", iconGuidMiddle),
                    Tuple.Create("Back", iconGuidBack)
                );
            } 
            
            // Add/Update custom build properties in pbxProject file (not in info.plist)
            if (customSettings is {customBuildProperties: {Count: > 0}})
                foreach (var customBuildProperty in customSettings.customBuildProperties)
                    if(customBuildProperty.IsEnabled)
                        pbxProject.SetBuildProperty(targetGuid, customBuildProperty.key, customBuildProperty.value);
            
            //Audio Fix for VisionOS
            if (buildTarget == BuildTarget.VisionOS && customSettings is { visionOsAudioFix: true })
                AudioFixVisionOs(pathToBuiltProject);
            
            #region Process Frameworks

            Debug.Log($"AppleBuild: OnProcessFrameworks begin...");

            foreach (var buildStep in appleBuildProfile.buildSteps)
            {
                if (buildStep.Value.IsEnabled)
                {
                    Debug.Log($"AppleBuild: OnProcessFrameworks for step: {buildStep.Key}");
                    buildStep.Value.OnProcessFrameworks(appleBuildProfile, buildTarget, pathToBuiltProject, pbxProject);
                }
            }

            if (pbxProject != null)
            {
                pbxProject.WriteToFile(pbxProjectPath);
            }

            Debug.Log($"AppleBuild: OnProcessFrameworks end.");

            #endregion // Process Frameworks
            
            #region Finalize Post Process

            Debug.Log($"AppleBuild: OnFinalizePostProcess begin...");

            foreach (var buildStep in appleBuildProfile.buildSteps)
            {
                if (buildStep.Value.IsEnabled)
                {
                    Debug.Log($"AppleBuild: OnFinalizePostProcess for step: {buildStep.Key}");
                    buildStep.Value.OnFinalizePostProcess(appleBuildProfile, buildTarget, pathToBuiltProject);
                }
            }

            Debug.Log($"AppleBuild: OnFinalizePostProcess end.");

            #endregion // Finalize Post Process

        }

        /// <summary>
        /// Unity's plist modification often adds spaces which cause issue with formatting with the entitlements file.
        /// </summary>
        /// <param name="entitlementsPath"></param>
        private static void FixEntitlementFormatting(string entitlementsPath)
        {
            if (!File.Exists(entitlementsPath))
                return;

            Debug.Log($"AppleBuild: Fixing entitlements formatting begin...");

            var contents = File.ReadAllText(entitlementsPath);

            // We replace any <tag /> with a space and remove the space...
            var matches = Regex.Matches(contents, "<(.*)\\s/>");

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    contents = contents.Replace(match.Groups[0].Value, $"<{match.Groups[1].Value}/>");
                }
            }

            File.WriteAllText(entitlementsPath, contents);

            Debug.Log($"AppleBuild: Fixing entitlements formatting end.");
        }

        #region Public Utility Methods

        /// <summary>
        /// Returns true if the Unity project settings are configured to create an Xcode project for the StandaloneOSX build target
        /// </summary>
        public static bool IsXcodeGeneratedMac()
        {
            return EditorUserBuildSettings.GetPlatformSettings("OSXUniversal", "CreateXcodeProject") == "true";
        }

        /// <summary>
        /// Returns the path to the info.plist for a given build target and project
        /// </summary>
        public static string GetInfoPlistPath(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.StandaloneOSX)
            {
                if (IsXcodeGeneratedMac())
                {
#if UNITY_2020_1_OR_NEWER
                    return $"{pathToBuiltProject}/{Application.productName}/Info.plist";
#else
                    return $"{Path.GetDirectoryName(pathToBuiltProject)}/{Application.productName}/Info.plist";
#endif
                }
                else
                {
                    return $"{pathToBuiltProject}/Contents/Info.plist";
                }
            }
            else
            {
                return $"{pathToBuiltProject}/Info.plist";
            }
        }

        /// <summary>
        /// Returns the path to the entitlements for a given build target and project
        /// </summary>
        public static string GetEntitlementsPath(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.StandaloneOSX)
            {
                if (IsXcodeGeneratedMac())
                {
#if UNITY_2020_1_OR_NEWER
                    return $"{pathToBuiltProject}/{Application.productName}/{Application.productName}.entitlements";
#else
                    return $"{Path.GetDirectoryName(pathToBuiltProject)}/{Application.productName}/{Application.productName}.entitlements";
#endif
                }
                else
                {
                    return $"{Path.GetDirectoryName(pathToBuiltProject)}/{Application.productName}.entitlements";
                }
            }
            else
            {
                return $"{pathToBuiltProject}/{Application.productName}.entitlements";
            }
        }

        /// <summary>
        /// Returns the Xcode scheme name for a given BuildTarget
        /// </summary>
        public static string GetSchemeName(BuildTarget buildTarget)
        {
            switch(buildTarget){
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                    return "Unity-iPhone";
                case BuildTarget.VisionOS:
                    return "Unity-VisionOS";
                case BuildTarget.StandaloneOSX:
                    return Application.productName;
                default:
                    return "Unity-iPhone";
            }
            return buildTarget == BuildTarget.StandaloneOSX ? Application.productName : "Unity-iPhone";
        }

        /// <summary>
        /// Utility method for getting the SDK name for a given BuildTarget
        /// </summary>
        public static string GetSDKName(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.iOS:
                    return "iphoneos";
                case BuildTarget.VisionOS:
                    return "visionos";
                case BuildTarget.tvOS:
                    return "appletvos";
                case BuildTarget.StandaloneOSX:
                    return "macosx";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Utility method for getting the .xcodeproj path for a built project
        /// </summary>
        public static string GetXcodeProjectPath(BuildTarget buildTarget, string pathToBuiltProject)
        {
            switch (buildTarget)
            {
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                    return $"{pathToBuiltProject}/Unity-iPhone.xcodeproj";
                case BuildTarget.VisionOS:
                    return $"{pathToBuiltProject}/Unity-VisionOS.xcodeproj";
                case BuildTarget.StandaloneOSX:
#if UNITY_2020_1_OR_NEWER
                    return $"{pathToBuiltProject}/{new DirectoryInfo(pathToBuiltProject).Name}.xcodeproj";
#else
                    return pathToBuiltProject;
#endif
                default:
                    return null;
            }
        }

        /// <summary>
        /// Utility method for getting the .pbxproj path for a built project
        /// </summary>
        public static string GetPbxProjectPath(BuildTarget buildTarget, string pathToBuiltProject)
        {
            switch (buildTarget)
            {
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                    return PBXProject.GetPBXProjectPath(pathToBuiltProject);
                case BuildTarget.VisionOS:
                    return PBXProject.GetPBXProjectPath(pathToBuiltProject).Replace("Unity-iPhone", "Unity-VisionOS");  //!Fix due Unity Method PBXProject.GetPBXProjectPath dont return a correct path for VisionOS
                case BuildTarget.StandaloneOSX:
#if UNITY_2020_1_OR_NEWER
                    return $"{GetXcodeProjectPath(buildTarget, pathToBuiltProject)}/project.pbxproj";
#else
                    return $"{pathToBuiltProject}/project.pbxproj";
#endif
                default:
                    return null;
            }
        }

        /// <summary>
        /// Utility method for getting a PBXProject object associated with the provided BuildTarget and built project path
        /// </summary>
        public static PBXProject GetPbxProject(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.StandaloneOSX && !IsXcodeGeneratedMac())
            {
                return null;
            }

            // Load the project...
            var proj = new PBXProject();
            proj.ReadFromFile(GetPbxProjectPath(buildTarget, pathToBuiltProject));

            return proj;
        }

        /// <summary>
        /// Allows all AppleBuildSteps in the project to optionally respond to updated exportPlistOptions when called from another build step
        /// </summary>
        public static void ProcessExportPlistOptions(AppleBuildProfile appleBuildProfile, BuildTarget buildTarget, string pathToBuiltProject, PlistDocument exportPlistOptions)
        {
            Debug.Log($"AppleBuild: ProcessExportPlistOptions begin...");

            foreach (var buildStep in appleBuildProfile.buildSteps)
            {
                if (buildStep.Value.IsEnabled)
                {
                    buildStep.Value.OnProcessExportPlistOptions(appleBuildProfile, buildTarget, pathToBuiltProject, exportPlistOptions);
                }
            }

            Debug.Log($"AppleBuild: ProcessExportPlistOptions end.");
        }

        #endregion // Public Utility Methods
        
        private static void AudioFixVisionOs(string pathToBuiltProject)
        {
            string unityAppControllerPath = Path.Combine(pathToBuiltProject, "Classes/UnityAppController.mm");
            if (File.Exists(unityAppControllerPath))
            {
                string[] lines = File.ReadAllLines(unityAppControllerPath);

                // Find the line with "[AVAudioSession sharedInstance]"
                int index = Array.FindIndex(lines, line => line.Contains("[AVAudioSession sharedInstance];"));
                if (index != -1)
                {
                    lines[index] += "\n\t// Audio fix for VisionOS/Unity (temporal) - Disable visionOS spatial audio\n\t" + 
                                    "[audioSession setIntendedSpatialExperience:AVAudioSessionSpatialExperienceBypassed options:@{} error:nil];";
                    File.WriteAllLines(unityAppControllerPath, lines);
                }
                else
                {
                    Debug.LogWarning("UnityAppController.mm: Unable to find the line '[AVAudioSession sharedInstance]'.");
                }
            }
            else
            {
                Debug.LogWarning("UnityAppController.mm not found.");
            }
        }

    }
}
#endif

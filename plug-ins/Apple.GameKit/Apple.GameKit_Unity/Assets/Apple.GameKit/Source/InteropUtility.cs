﻿namespace Apple.GameKit
{
    internal static class InteropUtility
    {
#if UNITY_IOS || UNITY_VISIONOS || UNITY_TVOS
        public const string DLLName = "__Internal";
#else
        public const string DLLName = "GameKitWrapper";
#endif
    }
}
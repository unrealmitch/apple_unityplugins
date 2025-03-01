//
//  GameKitWrapper.h
//  GameKitWrapper
//
//  Copyright © 2021 Apple, Inc. All rights reserved.
//
#pragma clang system_header
#import <Foundation/Foundation.h>

//! Project version number for GameKitWrapper.
FOUNDATION_EXPORT double GameKitWrapperVersionNumber;

//! Project version string for GameKitWrapper.
FOUNDATION_EXPORT const unsigned char GameKitWrapperVersionString[];

//! iOS & tvOS Frameworks do not support bridging headers...
#if TARGET_OS_IOS || TARGET_OS_TV || TARGET_OS_VISION
    #include <stdbool.h>
    #import <GameKitWrapper/AppleCoreRuntimeShared.h>
    #import <GameKitWrapper/AccessPoint_BridgingHeader.h>
#endif

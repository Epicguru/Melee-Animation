## Melee Animation Mod
A Rimworld mod that adds detailed melee combat animations, lassos, and animated melee-related skills.

You can download the [latest release here](https://github.com/Epicguru/AdvancedAnimationMod/releases/latest) or get it [on Steam here](https://steamcommunity.com/sharedfiles/filedetails/?id=2944488802).

[Click here to see a non-exhaustive list of compatible mods.](https://github.com/Epicguru/AdvancedAnimationMod/blob/master/WeaponTweakData/Compatible%20Mods.md)
Other mods may be compatible. The mods listed above are just the mods that have patches made by Epicguru.

[Click here to see how to create a patch yourself!](Source/TweakTutorial/AuthorTweaks.md)

## For modders and maintainers
Legal: please see the LICENSE for what you can and can not do with this mod's source code and assets.  
I would appreciate being contacted if you plan to do a major fork or continuation of this mod: I can be contacted by opening an issue on this Github page, or on discord under the @epicguru handle.

### How to build
Clone this repository into your Mods folder.  
The recommended IDE is Rider, but Visual Studio should also work. Visual Studio Code (VS Code) will *NOT* work!  
There is a seperate solution file and folder for each major Rimworld version, such as `1.4` and `1.5`.  
Simply open the solution for the current version and build the entire solution (`Ctrl+Shift+B` will build everything).  
There is no need to link up any assembly or project references, it is all done via NuGet packages.  
All the assemblies will be put into the correct folders automatically.

### Creating new animations
See the [Animation Creation Guide](Source/AnimationTutorial/AnimationTutorial.md).
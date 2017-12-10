# ResearchCore
Extensible item unlocking system for Space Engineers

This mod allows you to setup a custom research system on a per world basis, share your system with others as a Steam Workshop Mod, 
and include modded blocks in your research trees without fear.

[Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=1227335743)

## Features
- Lock blueprints (assembler recipes and ore refining) behind long and complicated tech trees
- Make people suffer through gathering millions of kilograms of Silicon ore before they can build a programmable block.
- Impose your own, custom, arbitrary limits on players who join your world without publishing a mod
- Mix and match the most evil research trees on the Steam Workshop so only the truly dedicated have a chance of survival

## Configuration - Importing Workshop Mods
To get a mod pack providing research into your world its as easy as subscribing to this (ResearchCore) mod and the research pack from 
the Steam Workshop.

## Configuration - Making Custom Rules
Making custom rules requires an understanding of XML and a passing familiarity with navigating the SE Content files 
(Primarily `Blueprints.sbc`, `BlueprintClasses.sbc`, and `CubeBlocks.sbc`) to determine the definition ID of the items you want to block.
Then, following the [research definition reference](https://github.com/Equinox-/ResearchCore/blob/master/examples/reference.xml) 
you create your own research tree, or amend another tree (instructions on amending soon).
To load this file into your world it's as simple as naming it `aux_research.xml`, and placing it in this mod's world storage folder 
(`YourSaveFolder/Storage/1227335743.sbm_ResearchCore/aux_research.xml`).
Reload the world and your research should be in use, and those overpowered blocks you added locked behind hours of progression.  
If it isn't there you can view the `ResearchCore.log` file located in the same folder for information about the error.

So now you want to publish your truly diabolical research tree on the Steam Workshop?  
Sadly SE doesn't have support for easy cross-mod definition sharing, so some tricks are needed.  
This mod will take any prefab that has a name starting with `EqResearch_`, find all the programmable blocks in the prefab, 
and load the information located in the Program field as Base64 coded byte array, parse it into a UTF8 string, and convert
it to the XML found in the reference document.  Example coming soon.

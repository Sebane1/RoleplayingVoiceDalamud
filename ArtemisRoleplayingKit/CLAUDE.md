Our respository is the following.
https://github.com/Sebane1/RoleplayingVoiceDalamud

Avoid using the above repository for cross referencing information as its our own code. We edit that.

This project relies on the following framework to run and operates as a plugin.
Check and see if Dalamud related nuget packages are up to date, and refere to the repository below to ensure our code aligns. Its not uncommon for major changes to the API to occur and for many things to break as a result.
Be prepared to alter and fix our code to align with new API changes.
https://dalamud.dev/api/
https://github.com/goatcorp/Dalamud

Roughly every 4 months new patches are released that break critical aspects of this plugin, and requires updating memory signatures as a result.
Some classes will use memory offset attributes instead of signatures.
This projects borrows memory signatures from multiple projects. Be sure to validate our memory signatures and offsets match up to the following respositories as reference
https://github.com/imchillin/Anamnesis
https://github.com/ktisis-tools/Ktisis
https://github.com/Cytraen/SoundFilter
https://github.com/MgAl2O4/PatMeDalamud
https://github.com/karashiiro/TextToTalk
https://github.com/PunishedPineapple/WhatDidYouSay

This project relies on API's from the following plugins, make sure our plugin endpoints still match. Updating Nuget packages, and updating our code is typically enough.
https://github.com/xivdev/Penumbra
https://github.com/Ottermandias/Glamourer

Unforseen performance impacts to our code appear to occur after major patches to Dalamud, and the game it runs on. Be sure to meticulously check for any obvious causes of performance bottlenecks.
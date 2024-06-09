# Package Processor
Make it less tedious to import or export only the things that you need.

### [Download From Here](https://vpm.dreadscripts.com/)

![image](https://cdn.discordapp.com/attachments/1096062312495972414/1096062312944783450/postprost.gif?ex=66343bd3&is=6632ea53&hm=3bec6fe57e3675b2a80bba600fcbb753f312ec7797d62ed5633d3defb8c09b21&)

Package Processor can be used to reduce the tediousness of having to Select/Deselect certain assets based on Exclusion rules and other features.

Settings can be found under
DreadTools > Scripts Settings > Package Processor
Currently, Import Processor only deselects scripts and doesn't have settings.

Settings Window
---------------
- "Active": Whether the settings should affect the exporting at all.
	- "Include Dependencies": Whether opening the export window should have dependencies On or Off by default.
	- "Default Off Extensions": Any File matching one of these extensions will be Off by default.
	- "Default Off Folders": Any File whose path contains one of these paths will be Off by default.
	- "Default Off Types": Any File whose type matches one of these types will be Off by default.
	- "Default Off Assets":	Any File whose GUID matches one of these GUIDs will be Off by default.
	- "Settings Path": The path where the settings should be saved and loaded from.

Export Window
-------------
Right Clicking any file will open a Context menu for extra features

- File - Toggle Type: Turns On/Off all of the assets being exporting whose types match the one selected.
- Exclusions:
	- Add/Remove Asset: Adds/Removes this File's GUID in the Default Off Assets list.
	- Add/Remove Type: Adds/Removes this File's Type in the Default Off Types list.
	- Add/Remove Extension: Adds/Removes this File's Extension in the Default Off Extensions list.

- Folder - Exclusions: Add/Remove Folder: Adds/Removes this Folder's Path in the Default Off Folders list.

**Requires Harmony!** 

### Thank you
If you enjoy Package Processor, please consider [supporting me ♡](https://ko-fi.com/Dreadrith)!

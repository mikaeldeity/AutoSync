# AutoSync

Automatically sync Revit documents.

## Behavior
- Relinquish every 15 minutes
- Sync with Central every 120 minutes

---

## Install

Download **AutoSync.zip** from the latest release  
2. Unzip the file  
3. Right click **AutoSync.dll** → Properties → check **Unblock** → Apply  
4. Inside the extracted zip, open the folder that matches your Revit version and framework  
   (for example **net48** or **net8.0-windows**)  
5. Copy **all contents of that folder** into the corresponding Revit Addins directory  
   under `C:\ProgramData\Autodesk\Revit\Addins\<RevitVersion>`

After copying the files, the following paths must exist:

`C:\ProgramData\Autodesk\Revit\Addins\<RevitVersion>\AutoSync.addin`

`C:\ProgramData\Autodesk\Revit\Addins\<RevitVersion>\AutoSync\AutoSync.dll`

---

## Revit Addins folders

### net48
Use this for Revit 2018–2024 running on .NET Framework.
Copy the **contents** of the **net48** folder into the matching Revit version folder.

`C:\ProgramData\Autodesk\Revit\Addins\2018`

`C:\ProgramData\Autodesk\Revit\Addins\2019`

`C:\ProgramData\Autodesk\Revit\Addins\2020`

`C:\ProgramData\Autodesk\Revit\Addins\2021`

`C:\ProgramData\Autodesk\Revit\Addins\2022`

`C:\ProgramData\Autodesk\Revit\Addins\2023`

`C:\ProgramData\Autodesk\Revit\Addins\2024`

### net8.0-windows
Use this for Revit versions that support .NET 8.
Copy the **contents** of the **net8.0-windows** folder into the matching Revit version folder.

`C:\ProgramData\Autodesk\Revit\Addins\2025`

`C:\ProgramData\Autodesk\Revit\Addins\2026`

## Notes
- Do not mix **net48** and **net8.0-windows** files in the same Revit Addins folder  
- Revit must be restarted after installation  
- The addin runs automatically once Revit is open

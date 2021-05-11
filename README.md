# AwesomeService
Small project to familiarise myself with .net 5.0 services.

Project is designed as a service running with system permissions

Current functions:

Adds functionality to Microsoft Snip & Sketch
- Copies image files from temp folder to the users "My Pictures" folder.
Function looks for the temp folder for all users and create a filewatcher foreach folder. It will then look up the users enviroment variables to identify the correct destination.

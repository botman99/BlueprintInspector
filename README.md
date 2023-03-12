
# Blueprint Inspector


## Getting set up:

1. First things first.  Make sure Intellisense is enabled.  Blueprint Inspector uses [Microsoft's Visual Studio CodeLens](https://learn.microsoft.com/en-us/visualstudio/ide/find-code-changes-and-other-history-with-codelens?view=vs-2022) to display information about which Unreal Blueprint assets use which native C++ functions.  CodeLens **requires** [C++ Intellisense](https://learn.microsoft.com/en-us/visualstudio/ide/visual-cpp-intellisense?view=vs-2022) to be enabled.  To check if Intellisense is enabled or disabled, in Visual Studio, click on 'Tools' -> 'Options' in the Visual Studio main menu and scroll down to 'Text Editor' and expand it.  Then expand 'C/C++' and click on 'Advanced'.  In the 'Browsing/Navigation' section, you want 'Disable Database' to be set to 'False' (along with the other 'Disable' settings from 'Disable Database Updates' to 'Disable External Dependencies Folders').  Once these are set to 'False', click the 'OK' button to close that dialog.

2. Install the [Blueprint Inspector](https://www.unrealengine.com/marketplace/en-US/product/blueprintinspector) Unreal Engine plugin.

3. Enable the Blueprint Inspector plugin by running the Unreal editor and use 'Edit' -> 'Plugins' from the editor main menu to search for the "Blueprint Inspector" plugin and check the 'Enabled' checkbox, then click the 'Restart Now' button.

4. Install the 'BlueprintInspector' extension for Visual Studio [2019](https://marketplace.visualstudio.com/items?itemName=JeffBroome.BlueprintInspector2019) or [2022](https://marketplace.visualstudio.com/items?itemName=JeffBroome.BlueprintInspector2022).

5. Open the Unreal Solution file (.sln) for your project in Visual Studio.

6. Set up CodeLens for Blueprint Inspector by clicking 'Tools' -> 'Options' in the Visual Studio main menu and scroll down to 'Text Editor' and expand it.  Then expand 'All Languages' and click on 'CodeLens'.  You should see a list of CodeLens features that can be enabled or disabled with a checkbox.  Make sure that the
'Enable CodeLens' checkbox at the top is checked and make sure that the 'Show Blueprint Inspector' checkbox is checked and click 'OK' (you can disable all the rest of these if you don't use them).

7. Now you need to generate the JSON file that stores the Blueprint asset information used by the Visual Studio Blueprint Inspector extension.  Click on 'Extensions' in the Visual Studio main menu, then in the 'Blueprint Inspector' menu item, click on 'Generate JSON File'.  You should see something like this:

![GenerateJsonFile](/images/GenerateJsonFile.png)

You will see some checkboxes that can be unchecked to exclude some of the Blueprint assets from the JSON file.  This will prevent those Blueprint assets from showing up in the Blueprint Inspector CodeLens information.  This can be used if you wish to filter out Blueprint assets from the Engine Content folder or from any Plugins that you may have installed from the Epic Games Marketplace.  You can also exclude Blueprint Assets from any 'Developers' folders (if you have developers that are creating test content in their own directories).  The Blueprint assets from the project's Content folder (i.e. your game) will always be included in the JSON file.

Once you have the checkboxes set the way you want, click the 'OK' button to start running the Blueprint Inspector commandlet that will generate the JSON file.

The Blueprint Inspector commandlet should take less than a minute to run on very small projects (depending on your computer's CPU and disk speed).  For very large projects with tens of thousands of Blueprint assets, the commandlet can take 10 or 15 minutes to run (or longer).

When the Blueprint Inspector commandlet starts, it will open a new pane in the Visual Studio 'Output' window where you can watch the progress of the commandlet as it runs.  You can see this output in the Output window by selecting 'Blueprint Inspector' from the "Show output from:" dropdown in the Output window.

When the Blueprint Inspector commandlet finishes, it will pop up a dialog box letting you know that the commandlet is done.  It will notify you in this dialog box if you need to restart Visual Studio to be able to display the updated JSON file information.

The JSON file will be placed in the hidden '.vs' folder in your project's root folder (where the .sln file is located).  This make it persistent so that you don't have to generate it each time Visual Studio is started up.


**Important!**

You WILL NEED to run this commandlet (via the Extensions -> Blueprint Inspector -> Generate JSON File) each time you have added, modified or removed any Blueprint assets and each time you have added, modified or removed any Blueprint native C++ functions.  You don't have to keep running this commandlet if you don't care that the Blueprint CodeLens information is out of date.  You only need to run the commandlet when you want to make sure that everything is up to date.  This is a manual process because of the amount of time it takes to run this commandlet for projects that a have a large number of Blueprints and don't want to have to tie up Visual Studio running this commandlet automatically each time Visual Studio starts up.

## How to use Blueprint Inspector in Visual Studio:

When you open an Unreal .h file or .cpp file that contains Blueprint native C++ functions, Intellisense will parse the file and then CodeLens will display information about Blueprint functions in that file.  Depending on the speed of your computer, this process can take a while.  You can tell when files are being scanned by looking at the 'Background tasks' button in the lower left corner of Visual Studio.  Any time you see that icon doing activity, you know that things are being updated.  Clicking on the 'Background tasks' button (or using Ctrl-E, Ctrl-T) will show you a list of which background tasks are active.  The CodeLens information will show up shortly after the background tasks are complete.  For example, you may see something like this...

![BackgroundTasks](/images/BackgroundTasks.png)

Once the file(s) have been parsed, you should see the CodeLens information for Blueprint assets soon after that.  For example, I have opened the Actor.h header file and scrolled down to see some of the Blueprint functions in that header file...

![BlueprintInspector_1](/images/BlueprintInspector_1.png)

Here you can see that 'K2_DestroyActor' is used by 2 Blueprint assets, 'HasAuthority' is used by 1 Blueprint asset, and 'AddComponent' is used by 10 Blueprint assets.

To see the list of Blueprint assets using that function, click on the CodeLens "x Blueprint assets" item immediately above the function declaration.  You would see something like this...

![BlueprintInspector_2](/images/BlueprintInspector_2.png)

From this pop up, you can copy the list of Blueprint assets (along with the class name and function name) to the Windows clipboard and paste that into a text file.  If the Unreal editor is running, you can double click on one of these Blueprint assets and the editor will automatically open that Blueprint asset and focus on the Node for that function in the Blueprint (if there are more than one matching Node, it will only focus on the first instance of that Node that it finds but you can use the 'Find in Blueprints' feature to search for the other uses of that function name).

In the upper right corner of that CodeLesn pop up, there is a 'Dock Popup' button that you can click on if you want to open this list in the docked 'CodeLens Blueprint Inspector - C++ Unit Test' window.  This window also has a "Copy all to clipboard" button but it will not contain the class name and function name for later reference.

![BlueprintInspector_3](/images/BlueprintInspector_3.png)

If you find it annoying that CodeLens keeps shuffling around text in the Visual Studio text editor while it displays Blueprint information, you can disable the 'Show Blueprint Inspector' item in the 'Tools' -> 'Options' -> 'Text Editor' -> 'All Languages' -> 'Code Lens' dialog to temporarily turn off showing CodeLens information for Blueprint Inspector and this will prevent CodeLens from shuffling the text around in the editor when you open files.

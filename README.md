# Valorant Overlay
## Installation and Use (Windows PC only)
To the right of the files, click on the release, download, and extract the zip wherever is convenient. Run the VALORANT Overlay.exe file, and click on more info -> run anyway if a windows prompt pops up. After a couple seconds, the exe should run.

Once the program is running, you should be able to press fn+F2 or simply F2 depending on your keyboard to pull up the settings. There will be three boxes in the top left and an "exit" text box in the top right. Click and drag to move the boxes around, and press F2 again to save your changes. If you click and drag on the **bottom right** of the boxes, you should be able to resize them. To exit the program, you must click **directly on the text** of the exit button. 

Your Valorant **must** be in borderless windowed or windowed, **not** fullscreen. Unfortunately Valorant doesn't allow any sort of process access (kernel anticheat will ban you), which stops us from changing its "Topmost" property that is turned on when in Fullscreen is selected, resulting in the overlay becoming an underlay.

If something in the program fails (it won't crash or cause an infinite loop, but might go behind other programs if they have admin always on top privileges which would make the exit button unclickable), just press ctrl+alt+delete, go to task manager, and search Overlay or VALORANT and select "VALORANT Overlay" and click end task. 
After editing your settings, they should look something like this:
<img width="1918" height="1198" alt="example-valorant-overlay" src="https://github.com/user-attachments/assets/941fb264-8bbc-4aca-a09f-ffea7ea1c88e" />
## Notes
After having the idea for this project, my first thought was to use the win32 c++ library to create a window like I had done for a previous project, but I realized that doing that both wouldn't be a useful way to increase the breadth of my skills and would be much less efficient then using a slightly simpler library like System.Windows from c#, which I ultimately decided on after a small amount of research.

I started by creating some basic files like the xaml and xaml.cs files needed by following a tutorial online, then building with dotnet as soon as I had a skeleton. After that, there were three main challenges to face. Window creation and configuration, Kill and weapon swap detection (and the animation to follow which was mostly straightforward), and settings within the app to tweak.

### Window creation
I had some experience with window creation that carried over, but I mostly had to follow online advice for creating an always on top window with no title bar. I added an idle mode when Valorant was closed and also registered the window to run on startup, both of which I decided to leave out in the end for simplicity and processing power concerns if something went wrong. Mainly I just had to iterate a lot to keep everything in the canvas always working and make the window click through but also clickable (more on that later). Pretty boring. However, I ran into a wall when I tried to run my prototype on Valorant in fullscreen, an obstacle that also stymied me when working on kill and weapon detection.

### Event detection
As noted in installation, Valorant doesn't allow any sort of process access, so I was required to run Valorant in windowed borderless for this to work. I also couldn't get the kill and weapon swap events directly from Valorant, as that would also trigger the anticheat. A service called Overwolf *does* have this access through a deal with Valorant, and I could use their API for kill detection if the user has that app installed, but this both doesn't solve weapon swap detection and I didn't want to require a third party service because it's more fun to make something standalone that can be bundled into a release.

So, I had to rely on what players themselves could see on screen to identify when they get a kill and swap weapons. First, kill detection. In Valorant, there's a kill feed at the top right of the screen, and a unique yellow border appears when it's your kill (or someone you're spectating's kill), shown below.
<img width="1199" height="346" alt="valorant yellow border example" src="https://github.com/user-attachments/assets/be1cf54c-5f1d-40c6-bc8f-66df243275b9" />
This border shows on the left side when you get a kill, and the right when you get killed, so incidentally it wouldn't be hard to add another detection for when you are killed. Getting detection working was fairly simple: I just had to capture the detection region every half second or so, check if there was a line of rgb value (236, 231, 119) pixels in that region, and play an animation if there was a kill, shown below in action.
![Kill-gif-example-compressed](https://github.com/user-attachments/assets/615d0b05-cf00-4258-884d-9b08855cc753)

There were a couple problems with detection, mostly solved by increasing tolerance, but by attempting to stop kills being detected again before they leave the region, kills in quick succession usually won't trigger the animation again, which is a complicated problem to solve correctly, so I just left it there.

For weapon detection, I had two main options. I could use supervised learning to train image recognition AI, using a dataset of labeled images of melee/sidearm/main weapons, or I could use pixel detection again to just check for the color of white that shows up on the weapon bar on the right when you switch weapons to detect which weapon you're currently using (shown below).
<img width="1918" height="1198" alt="weapon swap testing main" src="https://github.com/user-attachments/assets/68c1c410-141a-46cd-bde6-bd07ae82f405" />
vs
<img width="1918" height="1198" alt="weapon swap testing pistol" src="https://github.com/user-attachments/assets/e54b5814-813b-4823-91b6-c0470685783a" />


I chose pixel detection for reasons like performance and simplicity, but did sacrifice partial accuracy, as there are some in game objects that are the specific shades of white used on the weapon labels (specifically the pre-round barrier edges and one agent's (jett) hands in sunlight) that trigger the detection when entering the region and there wasn't much I could do about it.

This strategy overall worked quite well, but for different screen resolutions the regions and size of the animation had to be different, so I needed to add a "settings mode", activated with F2, that would allow you to move and resize regions.

### Settings mode

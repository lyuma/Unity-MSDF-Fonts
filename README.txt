FontMeshGen v0.2 booth source
https://lyuma.booth.pm/items/2034180

===============================

How to use FontMeshGen!

Please contact me Lyuma#0781 if you have any questions!
It is still very challenging to use for a new user. Please ask me if you have any issues. I will try to make it easier to use in the future.
~~~~~~~~~~~~~~~~~~~~~~~~~

Create a directory with MSDF text atlases, generated using Merlin's tool.
Here is my fork, which has a bugfix for Unity 2018, and generates .png files.
https://github.com/lyuma/Unity-MSDF-Fonts


First, set the Fonts to have these import settings:
Character = Custom set
Font Size = (20-30 depending on how many letters)
Custom Chars = put in all characters you plan to use.

Then, run Window->Merlin->MSDF Font Generator for each font.

*** CAVEAT ***
All fonts must be the *same* dimensions. Change the size of the font, add or remove letters until each image is the same size.

I will attempt to address this in a future update.
-----------------------

After this, Open Tools->Lyuma->FontMeshGen.

Select the Font dierctory.
Select the Atlas size. The total size width * height must match the number of fonts in the directory:
For example 2x2 for 4 fonts. You can also do something like 1x5 if you have 5 fonts.

Select the .txt file for strings
And finally, select an empty game object for the output mesh and armature.
ALL OBJECTS IN THIS MESH WILL BE CLEARED! Please use an empty object, or reuse generated text.

The script generates a Skinned Mesh which allows you to position objects ingame. Do note that rotating or moving objects will affect billboard or backface settings. Once you have good positions and rotations, you can update the transform values in the .txt and generate again. I will be improving this in the future.

It also outputs a mesh named ...._baked -> this is a non-skinned mesh which you can put into a standard MeshFilter/MeshRenderer.


------------------------
OKAY So how to make the txt file we keep talking about:


LIST OF TAGS:
** Per-letter settings
- <color=HTMLCOLOR>
(Set a color in unity format: red, white, #ccccffff, etc.)
(Please include an alpha component (8 letter hexcode))
- <colgrad=HTMLCOLOR>
(Color gradient: per-letter gradient, sets color= to this after the next character.)
- <shadow=HTMLCOLOR>
(Background shadow/glow around text)
- </shadow>
(Clear shadow, same as <shadow=#00000000> with 0 alpha.)
- <space=FLOAT>
(Scale size of space character)
- <size=FLOAT>
(Scale size of text or image)
- <blur=FLOAT>
(Create blurred text like shadow (1), normal (0) or extra sharp (-0.5))
- <lineheight=FLOAT>
(Line height multiplier, affects the next line)
- <stretch=FLOAT>
(Stretch text vertically, like size only on y axis)
- <fatten=FLOAT>
(Stretch text horizontally, like size only on x axis)
- <corrupt=FLOAT>
(Stretch one triangle in each quad, producing funny text)
- <extra=FLOAT>
(Activate custom shader feature, for example, float up and down slowly)
- <italic=FLOAT>
(Skew text by amount given: -0.25 = oblique, 0 = normal, 0.25 = italic, 1 = ??)
- <i>, </i>
(Same as italic=0.25 and italic=0)
- <thick=>
(Set letter thickness: 0=extremely thin, 0.5=normal, 1.0=bold)
- <b>, </b>
(Same as thick=1.0 and thick=0.5)
- <sep>, </sep>
(Broken: Please use fatten= instead, combined with _ for nice separators)

** Per-character positioning:
- <center>
- <left>
- <right>
- <align=FLOAT>
(Align text left (0) or right (1))
- <x=FLOAT>, <y=FLOAT> <z=FLOAT>
(Set the current x, y or z position of the text cursor, relative to the node.)
- <x=+FLOAT>, <y=+FLOAT> <z=+FLOAT>
(Set the current x, y or z position of the text cursor, relative to the previous value.)
- <angle=FLOAT>
(Set the angle of the text, relative to the node.)
- <anglexlate=FLOAT,X,Y,Z>
(Reset the angle and position of the cursor, relative to the node)
- <advangle=FLOAT>
(Set the angle of the cursor, affecting newly written characters.)

** Per-node commands:
- <parent=NAME>, </parent>
(Create a new game object to hold everything inside. No nesting!)
- <translate=X,Y,Z>
(Translate the game object.)
- <rotate=X,Y,Z>
(Rotate the game object in euler degrees.)
- <scale=X,Y,Z>
(Scale the game object.)
- <distancefade=FLOAT>
(Fade this object away as the user reaches this distance from the text on X/Z, ignoring Y.)
- <billboard>, </billboard>
(Billboard the current node towards the player camera)
- <doubleside>, </doubleside>
(Text will be correctly flipped from both sides.)
- <img=X1,Y1,X2,Y2>
(Reference an image from the Alternate Graphics slot in the material)
(X1,Y1 is bottom left; X2,Y2 is top right).
(The image will be scaled according to <size=>, <stretch=> and <fatten=> commands.)
- <blockimg=X1,Y1,X2,Y2>
(Like <image=> but the image will be centered on the cursor, instead of appended.)

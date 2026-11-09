// Appended after the original root frame 3 (menu sound and p1/p2 cleanup).
if(!xanaduBooted)
{
    xanaduBooted = true;
    xanaduSetSoundEnabled(xanaduDesiredSound);
    if(xanaduMode == "1" || xanaduMode == "2")
    {
        totalPlayer = Number(xanaduMode);
        // Exactly the original single / two-player button destination.
        gotoAndStop("menu3");
    }
    delete xanaduMode;
}
xanaduPublish();

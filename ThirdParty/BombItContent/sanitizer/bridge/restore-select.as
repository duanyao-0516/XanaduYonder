// Original frame 8 has created all four hero records and canonical key maps.
if(xanaduRestoring && xanaduCheckpointValid(xanaduRestore))
{
    for(var i = 1; i <= totalPlayer; i++)
    {
        this["p" + i].heroId = xanaduRestore.heroes[i - 1].heroId;
        this["p" + i].setDown = true;
    }
    gotoAndStop("game");
}

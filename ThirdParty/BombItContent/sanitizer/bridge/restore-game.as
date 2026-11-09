// Run AFTER original frame 9 resets stage/lives/score, BEFORE game sprite setup.
if(xanaduRestoring && xanaduCheckpointValid(xanaduRestore))
{
    stageNum = xanaduRestore.stage - 1;
    for(var i = 1; i <= 4; i++)
    {
        var saved = xanaduRestore.heroes[i - 1];
        var hero = this["heroObj" + i];
        hero.heroId = saved.heroId;
        hero.life = saved.life;
        hero.totalScore = saved.totalScore;
    }
}

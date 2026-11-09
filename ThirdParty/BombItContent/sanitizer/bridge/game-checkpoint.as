// Original gameover function is retained byte for byte. Capture only AFTER all
// its scoring, life changes, teammate revival and result selection have run.
xanaduOriginalGameover = gameover;
gameover = function(message)
{
    this.xanaduOriginalGameover(message);
    _root.xanaduFinishRound();
};

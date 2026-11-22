using System;
using System.Linq;
using XanaduYonder.Core.BakeryBasket;

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label);
}

var round = new BakeryRound(17);
var ids = Enumerable.Range(0, BakeryRound.PieceCount).ToArray();
var kinds = ids.Select(round.KindOf).ToArray();
Check(kinds.SequenceEqual(ids.Select(new BakeryRound(17).KindOf)) &&
    !kinds.SequenceEqual(ids.Select(new BakeryRound(18).KindOf)) &&
    BakeryRound.PieceCount == 180 && BakeryRound.KindCount == 15 &&
    kinds.GroupBy(kind => kind).OrderBy(group => group.Key).Select(group => group.Count())
        .SequenceEqual(new[] { 12, 12, 15, 15, 9, 12, 12, 15, 15, 9, 12, 9, 12, 12, 9 }),
    "same seed deals the same 180 pieces across 15 uneven kinds");
var groups = ids.GroupBy(round.KindOf).Select(group => group.ToArray()).ToArray();
for (var i = 0; i < 5; i++) round.Take(groups[i][0]);
round.Take(groups[0][1]);
var matched = round.Take(groups[0][2]);
Check(matched.SequenceEqual(groups[0].Take(3)) && round.Tray.SequenceEqual(groups.Skip(1).Take(4).Select(group => group[0])) &&
    round.Phase == BakeryPhase.Playing && round.Remaining == BakeryRound.PieceCount - 7, "seventh selection matches before a full tray loses");
Check(round.Take(groups[0][2]) == null && round.Take(-1) == null && round.Remaining == BakeryRound.PieceCount - 7,
    "duplicate and invalid picks are atomic");

round = new BakeryRound(17);
foreach (var group in groups.Take(7)) round.Take(group[0]);
Check(round.Phase == BakeryPhase.Full && round.Take(groups[7][0]) == null && round.Tray.Count == 7,
    "seven unmatched pieces end the round");

round = new BakeryRound(17);
foreach (var group in groups)
    foreach (var id in group) round.Take(id);
Check(round.Phase == BakeryPhase.Cleared && round.Remaining == 0 && round.Tray.Count == 0,
    "clearing all triples wins");

round = new BakeryRound(17);
var firstSeven = groups.Take(7).Select(group => group[0]).ToArray();
foreach (int id in firstSeven) round.Take(id);
int beforeMove = round.Remaining;
Check(round.CanMoveOut && round.MoveOut() && round.Phase == BakeryPhase.Playing &&
    round.Reserve.SequenceEqual(firstSeven.Take(3)) && round.Tray.SequenceEqual(firstSeven.Skip(3)) &&
    round.Remaining == beforeMove, "moving out the oldest three rescues a full tray without taking new pieces");
Check(round.MoveOut() && round.Reserve.SequenceEqual(firstSeven.Take(6)) &&
    round.Tray.SequenceEqual(firstSeven.Skip(6)) && !round.MoveOut(), "the six-slot reserve rejects an overflow atomically");
Check(round.Take(firstSeven[0]) == null, "a reserved piece is no longer in the basket");
round.Take(groups[0][1]);
round.Take(groups[0][2]);
int beforeReturn = round.Remaining;
matched = round.ReturnFromReserve(0);
Check(matched.SequenceEqual(new[] { groups[0][1], groups[0][2], firstSeven[0] }) &&
    round.Reserve.SequenceEqual(firstSeven.Skip(1).Take(5)) && round.Remaining == beforeReturn &&
    round.Tray.SequenceEqual(firstSeven.Skip(6)), "returning a reserved piece can complete a triple without changing the basket count");
Check(round.ReturnFromReserve(-1) == null && round.ReturnFromReserve(5) == null &&
    round.ReturnFromReserve(6) == null, "invalid reserve taps leave the round alone");
foreach (var group in groups.Skip(7).Take(6)) round.Take(group[0]);
Check(round.Phase == BakeryPhase.Full && round.MoveOut() && round.Reserve.Count == 6 &&
    round.Reserve[5] == firstSeven[6] && round.Tray.Count == 6 && round.Phase == BakeryPhase.Playing,
    "one free reserve cell can still rescue a full tray");

round = new BakeryRound(17);
foreach (var group in groups.Take(groups.Length - 1))
    foreach (var id in group) round.Take(id);
var last = groups[groups.Length - 1];
round.Take(last[0]);
Check(round.MoveOut() && round.Reserve.SequenceEqual(last.Take(1)), "a small tray can move its remaining items");
foreach (var id in last.Skip(1)) round.Take(id);
Check(round.Remaining == 0 && round.Phase == BakeryPhase.Playing && round.Reserve.Count == 1,
    "an empty basket still has to clear the reserve");
Check(round.ReturnFromReserve(0).Length == 3 && round.Phase == BakeryPhase.Cleared && round.Reserve.Count == 0,
    "clearing the last reserved piece finishes the round");

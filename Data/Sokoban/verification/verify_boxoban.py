#!/usr/bin/env python3
"""Bounded design-time Sokoban search and independent cell-by-cell replay.

Original verification tooling. Upstream level data remains Apache-2.0.
This is not Unity production code, a difficulty grader, or an optimal-move solver.
"""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import time

DIRECTIONS = (("L", -1, 0), ("U", 0, -1), ("R", 1, 0), ("D", 0, 1))
EXPECTED_SHA256 = "272928a4e7c185fdf84daa523b298750b6ff08703cb0c20d3be7eff93acc5256"


def parse_source(path):
    raw = path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != EXPECTED_SHA256:
        raise ValueError("Source SHA-256 differs from the reviewed archive")
    levels = []
    current = None
    for line in raw.decode("ascii").splitlines():
        if line.startswith("; "):
            if current is not None:
                levels.append(current)
            current = {"source_id": int(line[2:]), "rows": []}
        elif line:
            if current is None:
                raise ValueError("Map before source ID")
            current["rows"].append(line)
    if current is not None:
        levels.append(current)
    if [item["source_id"] for item in levels] != list(range(1000)):
        raise ValueError("Expected exactly 1,000 ordered source IDs")
    for item in levels:
        rows = item["rows"]
        flat = "".join(rows)
        if len(rows) != 10 or any(len(row) != 10 for row in rows):
            raise ValueError("Map dimensions must be 10 x 10")
        if set(flat) - set("# @$."):
            raise ValueError("Unsupported map symbol")
        if flat.count("@") != 1 or flat.count("$") != 4 or flat.count(".") != 4:
            raise ValueError("Expected one player, four boxes, four goals")
        if any(c != "#" for c in rows[0] + rows[-1]) or any(row[0] != "#" or row[-1] != "#" for row in rows):
            raise ValueError("Map boundary is not enclosed")
    return levels, digest


def search(rows, max_states, max_seconds):
    started = time.monotonic()
    floor = {y * 10 + x for y, row in enumerate(rows) for x, c in enumerate(row) if c != "#"}
    goals = {y * 10 + x for y, row in enumerate(rows) for x, c in enumerate(row) if c == "."}
    boxes = tuple(sorted(y * 10 + x for y, row in enumerate(rows) for x, c in enumerate(row) if c == "$"))
    player = next(y * 10 + x for y, row in enumerate(rows) for x, c in enumerate(row) if c == "@")
    moves = tuple((letter, dy * 10 + dx) for letter, dx, dy in DIRECTIONS)
    adjacent = {p: tuple((letter, p + delta) for letter, delta in moves if p + delta in floor) for p in floor}

    # Reverse-pull reachability gives only provably dead static box squares.
    viable = set(goals)
    queue = deque(goals)
    while queue:
        target = queue.popleft()
        for _, delta in moves:
            predecessor = target - delta
            if predecessor in floor and target - 2 * delta in floor and predecessor not in viable:
                viable.add(predecessor)
                queue.append(predecessor)

    def reachable(start, blocked):
        paths = {start: ""}
        pending = deque([start])
        while pending:
            pos = pending.popleft()
            for letter, nxt in adjacent[pos]:
                if nxt not in blocked and nxt not in paths:
                    paths[nxt] = paths[pos] + letter
                    pending.append(nxt)
        return paths

    initial_reach = reachable(player, set(boxes))
    nodes = [(boxes, player, -1, "")]
    seen = {(boxes, min(initial_reach))}
    queue = deque([0])
    expanded = 0
    while queue:
        if time.monotonic() - started > max_seconds:
            return {"status": "budget_exceeded", "limit": "seconds", "expanded": expanded, "discovered": len(nodes)}
        index = queue.popleft()
        current_boxes, current_player, parent, segment = nodes[index]
        if set(current_boxes) == goals:
            segments = []
            while index >= 0:
                _, _, previous, segment = nodes[index]
                segments.append(segment)
                index = previous
            solution = "".join(reversed(segments))
            return {"status": "solved", "moves_lurd": solution, "expanded": expanded, "discovered": len(nodes)}
        expanded += 1
        box_set = set(current_boxes)
        paths = reachable(current_player, box_set)
        for box in current_boxes:
            for letter, delta in moves:
                standing = box - delta
                destination = box + delta
                if standing not in paths or destination not in viable or destination in box_set:
                    continue
                new_boxes = tuple(sorted((box_set - {box}) | {destination}))
                new_reach = reachable(box, set(new_boxes))
                key = (new_boxes, min(new_reach))
                if key in seen:
                    continue
                if len(nodes) >= max_states:
                    return {"status": "budget_exceeded", "limit": "states", "expanded": expanded, "discovered": len(nodes)}
                seen.add(key)
                nodes.append((new_boxes, box, index, paths[standing] + letter))
                queue.append(len(nodes) - 1)
    # Keep failure reporting conservative; no generated level is labelled unsolvable.
    return {"status": "search_exhausted", "expanded": expanded, "discovered": len(nodes)}


def replay_independently(rows, solution):
    """No search adjacency/reachability/state helpers: direct 2D legal-move rules."""
    wall = {(x, y) for y, row in enumerate(rows) for x, tile in enumerate(row) if tile == "#"}
    goals = {(x, y) for y, row in enumerate(rows) for x, tile in enumerate(row) if tile == "."}
    boxes = {(x, y) for y, row in enumerate(rows) for x, tile in enumerate(row) if tile == "$"}
    player = next((x, y) for y, row in enumerate(rows) for x, tile in enumerate(row) if tile == "@")
    deltas = {"L": (-1, 0), "U": (0, -1), "R": (1, 0), "D": (0, 1)}
    pushes = 0
    for step, letter in enumerate(solution, 1):
        if letter not in deltas:
            raise ValueError(f"Illegal direction at step {step}")
        dx, dy = deltas[letter]
        target = player[0] + dx, player[1] + dy
        if not (0 <= target[0] < 10 and 0 <= target[1] < 10) or target in wall:
            raise ValueError(f"Walk into wall or outside at step {step}")
        if target in boxes:
            destination = target[0] + dx, target[1] + dy
            if not (0 <= destination[0] < 10 and 0 <= destination[1] < 10) or destination in wall or destination in boxes:
                raise ValueError(f"Illegal push at step {step}")
            boxes.remove(target)
            boxes.add(destination)
            pushes += 1
        player = target
    if boxes != goals:
        raise ValueError("Replay did not finish with all four boxes on goals")
    return {"steps": len(solution), "pushes": pushes, "final_player_xy": list(player), "final_boxes_xy": sorted([list(pos) for pos in boxes])}


def verify_saved(path, levels):
    document = json.loads(path.read_text())
    if document["source_sha256"] != EXPECTED_SHA256:
        raise ValueError("Witness source hash mismatch")
    ids = []
    for entry in document["levels"]:
        source_id = entry["source_id"]
        if entry["rows"] != levels[source_id]["rows"]:
            raise ValueError("Witness map differs from source")
        result = replay_independently(entry["rows"], entry["moves_lurd"])
        if result != entry["replay"]:
            raise ValueError("Witness replay counters or final state mismatch")
        ids.append(source_id)
    if ids != sorted(set(ids)):
        raise ValueError("Witness IDs must be unique and in source order")
    return len(ids)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--count", type=int, default=100)
    parser.add_argument("--max-states", type=int, default=35000)
    parser.add_argument("--max-seconds", type=float, default=1.5)
    parser.add_argument("--replay-only", action="store_true")
    args = parser.parse_args()
    directory = Path(__file__).resolve().parent
    source = directory.parent / "boxoban" / "unfiltered-test-000.txt"
    levels, digest = parse_source(source)
    output = directory / "verified-candidates.json"
    if args.replay_only:
        count = verify_saved(output, levels)
        print(f"REPLAY_PASS: {count} witnesses, source integrity and all 1000 level structures verified")
        return
    if args.count <= 0 or args.max_states <= 0 or args.max_seconds <= 0:
        raise ValueError("Search budgets and target count must be positive")
    started = time.monotonic()
    selected = []
    attempts = []
    for level in levels:
        result = search(level["rows"], args.max_states, args.max_seconds)
        attempt = {"source_id": level["source_id"], **{k: v for k, v in result.items() if k != "moves_lurd"}}
        attempts.append(attempt)
        if result["status"] == "solved":
            replay = replay_independently(level["rows"], result["moves_lurd"])
            selected.append({"id": f"boxoban-unfiltered-test-000-{level['source_id']:04d}", **level, "moves_lurd": result["moves_lurd"], "replay": replay})
        if len(attempts) % 10 == 0:
            print(f"Attempted {len(attempts)}, verified {len(selected)}", flush=True)
        if len(selected) == args.count:
            break
    witness = {"schema_version": 1, "purpose": "Solver-verified candidate pool; not a released or difficulty-graded campaign", "source_sha256": digest, "source_collection": "unfiltered/test/000.txt", "source_commit": "78011d8c86ad423bfb2e1005012393b4c3c24409", "level_data_license": "Apache-2.0", "move_encoding": "Uppercase L/U/R/D, each letter is one player step; push count computed by replay", "optimal_moves_claimed": False, "human_difficulty_graded": False, "levels": selected}
    output.write_text(json.dumps(witness, ensure_ascii=False, indent=2) + "\n")
    verified = verify_saved(output, levels)
    report = {"schema_version": 1, "source_levels_structurally_valid": len(levels), "requested_candidates": args.count, "verified_candidates": verified, "attempted_levels": len(attempts), "limits_per_level": {"max_discovered_states": args.max_states, "max_seconds": args.max_seconds}, "search": "Breadth-first over box pushes, canonical reachable player regions, static reverse-pull dead-square pruning", "selection": "First successful levels in original source order within per-level budgets", "duration_seconds": round(time.monotonic() - started, 3), "limitations": ["Search and replay are offline Python tooling, not Unity production rules", "No global player-step optimality claim", "No human difficulty or progression grading", "Unsearched and budget-exceeded maps have no independent solution proof here", "A time-based budget can make the selected subset vary across machines; checked-in witnesses are deterministic to replay", "No Unity, Android, animation, input, persistence or runtime acceptance performed"], "attempts": attempts}
    (directory / "verification-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(f"VERIFICATION_PASS: {verified} independently replayed solutions from {len(attempts)} attempted source levels", flush=True)
    if verified != args.count:
        raise SystemExit("Source exhausted before requested verified candidate count")


if __name__ == "__main__":
    main()

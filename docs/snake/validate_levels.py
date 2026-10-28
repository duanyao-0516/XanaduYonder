#!/usr/bin/env python3
"""Validate the snake research draft. This is not a game or a level solver.

Requires Python 3.9+ and only the standard library. Does not write files.
"""

import argparse
import copy
import json
import re
import sys
from collections import deque
from pathlib import Path


class ValidationError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise ValidationError(message)


def keys(value, expected, label):
    require(type(value) is dict, f"{label}: expected object")
    require(set(value) == set(expected), f"{label}: fields must be {sorted(expected)}")


def integer(value, minimum, maximum, label):
    require(type(value) is int, f"{label}: expected integer (not boolean)")
    require(minimum <= value <= maximum, f"{label}: expected {minimum}..{maximum}")


def coordinates(value, columns, rows, label):
    require(type(value) is list, f"{label}: expected coordinate array")
    result = []
    for index, point in enumerate(value):
        where = f"{label}[{index}]"
        require(type(point) is list and len(point) == 2, f"{where}: expected [x, y]")
        integer(point[0], 0, columns - 1, f"{where}.x")
        integer(point[1], 0, rows - 1, f"{where}.y")
        result.append(tuple(point))
    require(len(result) == len(set(result)), f"{label}: duplicate coordinates")
    return result


def adjacent(point, columns, rows, boundary):
    x, y = point
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if boundary == "wrap":
            yield nx % columns, ny % rows
        elif 0 <= nx < columns and 0 <= ny < rows:
            yield nx, ny


def open_square_cells(free, columns, rows, boundary):
    """Cells covered by at least one wholly open 2 x 2 square."""
    covered = set()
    for x, y in free:
        square = {(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)}
        if boundary == "wrap":
            square = {(px % columns, py % rows) for px, py in square}
        if square <= free:
            covered.update(square)
    return covered


def articulation_points(graph):
    """Find static one-cell bottlenecks with a depth-first graph traversal."""
    discovered, low, parent, cuts = {}, {}, {}, set()

    def visit(cell):
        discovered[cell] = low[cell] = len(discovered)
        children = 0
        for neighbor in graph[cell]:
            if neighbor not in discovered:
                parent[neighbor] = cell
                children += 1
                visit(neighbor)
                low[cell] = min(low[cell], low[neighbor])
                if cell in parent and low[neighbor] >= discovered[cell]:
                    cuts.add(cell)
            elif parent.get(cell) != neighbor:
                low[cell] = min(low[cell], discovered[neighbor])
        if cell not in parent and children > 1:
            cuts.add(cell)

    for cell in graph:
        if cell not in discovered:
            visit(cell)
    return cuts


def validate(data):
    """Return per-level statistics, or raise ValidationError at the first defect."""
    keys(data, ("schemaVersion", "draft", "rulesVersion", "packVersion", "defaults", "levels"), "root")
    integer(data["schemaVersion"], 1, 1, "schemaVersion")
    require(data["draft"] is True, "draft: must be true for this research draft")
    require(data["rulesVersion"] == "snake-rules-v1-draft", "rulesVersion: unsupported rules")
    require(type(data["packVersion"]) is str and bool(data["packVersion"].strip()),
            "packVersion: expected nonblank string")
    require(data["packVersion"] == "snake-levels-v1-draft-20261028", "packVersion: unsupported level pack")
    defaults = data["defaults"]
    keys(defaults, ("columns", "rows", "coordinateSystem", "initialSnake",
                    "initialDirection", "initialSafeSteps", "growthPerFood", "speedPolicy"), "defaults")
    integer(defaults["columns"], 20, 20, "defaults.columns")
    integer(defaults["rows"], 24, 24, "defaults.rows")
    columns, rows = defaults["columns"], defaults["rows"]
    system = defaults["coordinateSystem"]
    keys(system, ("origin", "indexBase", "xDirection", "yDirection"), "coordinateSystem")
    integer(system["indexBase"], 0, 0, "coordinateSystem.indexBase")
    require(system == {"origin": "top-left", "indexBase": 0, "xDirection": "right", "yDirection": "down"},
            "coordinateSystem: expected zero-based top-left origin, x right, y down")
    snake = coordinates(defaults["initialSnake"], columns, rows, "initialSnake")
    require(len(snake) == 4, "initialSnake: expected four cells, head first")
    require(all(abs(a[0] - b[0]) + abs(a[1] - b[1]) == 1 for a, b in zip(snake, snake[1:])),
            "initialSnake: consecutive cells must be orthogonally adjacent")
    require(snake == [(6, 20), (5, 20), (4, 20), (3, 20)], "initialSnake: unexpected starting layout")
    require(defaults["initialDirection"] == "right", "initialDirection: expected right")
    integer(defaults["initialSafeSteps"], 3, 3, "initialSafeSteps")
    integer(defaults["growthPerFood"], 1, 1, "growthPerFood")
    require(defaults["speedPolicy"] == "fixed-per-level", "speedPolicy: expected fixed-per-level")
    levels = data["levels"]
    require(type(levels) is list and len(levels) == 12, "levels: expected exactly 12 levels")
    board = {(x, y) for y in range(rows) for x in range(columns)}
    ids, names, stats = set(), set(), []
    chapters = ("01-basics", "02-islands", "03-dividers", "04-corridors")
    previous_tick, previous_target = 220, 8
    wrap_count = 0
    for index, level in enumerate(levels, 1):
        label = f"levels[{index - 1}]"
        keys(level, ("id", "name", "chapter", "boundary", "tickMs", "targetFood", "obstacles"), label)
        level_id = level["id"]
        require(type(level_id) is str and re.fullmatch(r"snake-\d{2}", level_id) is not None,
                f"{label}.id: expected snake-NN")
        require(level_id not in ids and level_id == f"snake-{index:02d}", f"{label}.id: duplicate or unexpected order")
        ids.add(level_id)
        name = level["name"]
        require(type(name) is str and 1 <= len(name.strip()) <= 40, f"{level_id}.name: expected 1..40 nonblank characters")
        require(name not in names, f"{level_id}.name: duplicate name")
        names.add(name)
        require(level["chapter"] == chapters[(index - 1) // 3], f"{level_id}.chapter: unexpected chapter")
        boundary = level["boundary"]
        require(boundary in ("wrap", "solid"), f"{level_id}.boundary: expected wrap or solid")
        wrap_count += boundary == "wrap"
        integer(level["tickMs"], 140, 220, f"{level_id}.tickMs")
        integer(level["targetFood"], 8, 30, f"{level_id}.targetFood")
        require(level["tickMs"] <= previous_tick, f"{level_id}: tickMs must not increase across levels")
        require(level["targetFood"] >= previous_target, f"{level_id}: targetFood must not decrease across levels")
        previous_tick, previous_target = level["tickMs"], level["targetFood"]
        obstacles = set(coordinates(level["obstacles"], columns, rows, f"{level_id}.obstacles"))
        require(not obstacles.intersection(snake), f"{level_id}: obstacle overlaps initial snake")
        if index <= 2:
            require(not obstacles, f"{level_id}: introductory board must have no obstacles")
            require(boundary == ("wrap" if index == 1 else "solid"), f"{level_id}: unexpected introductory boundary")
        free = board - obstacles
        capacity = (len(free) - len(snake)) // defaults["growthPerFood"]
        require(level["targetFood"] < capacity,
                f"{level_id}: targetFood must be strictly below free growth capacity {capacity}")
        x, y = snake[0]
        for step in range(1, defaults["initialSafeSteps"] + 1):
            landing = (x + step, y)
            if boundary == "wrap":
                landing = (landing[0] % columns, landing[1] % rows)
            require(landing in free and landing not in snake,
                    f"{level_id}: initial rightward step {step} is blocked")
        graph = {cell: tuple(n for n in adjacent(cell, columns, rows, boundary) if n in free)
                 for cell in sorted(free)}
        visited = {snake[0]}
        queue = deque(visited)
        while queue:
            for neighbor in graph[queue.popleft()]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(neighbor)
        require(visited == free, f"{level_id}: disconnected empty cells ({len(visited)}/{len(free)} reachable)")
        minimum_degree = min(map(len, graph.values()))
        require(minimum_degree >= 2, f"{level_id}: static dead end (minimum degree {minimum_degree})")
        uncovered = free - open_square_cells(free, columns, rows, boundary)
        require(not uncovered, f"{level_id}: cells outside open 2x2 areas: {sorted(uncovered)[:5]}")
        cuts = articulation_points(graph)
        require(not cuts, f"{level_id}: one-cell static bottlenecks: {sorted(cuts)[:5]}")
        stats.append(dict(id=level_id, boundary=boundary, tickMs=level["tickMs"], targetFood=level["targetFood"],
                          obstacles=len(obstacles), freeCells=len(free), growthCapacity=capacity,
                          reachable=len(visited), minDegree=minimum_degree, articulationPoints=len(cuts)))
    require(3 <= wrap_count <= 4, "levels: expected three or four wrap levels")
    return stats


def self_test(valid_data):
    """Reject malformed inputs and geometry; also verify the boundary topology."""
    cases = []

    def case(name, mutate, expected):
        data = copy.deepcopy(valid_data)
        mutate(data)
        cases.append((name, data, expected))

    case("boolean schema", lambda d: d.update(schemaVersion=True), "expected integer")
    case("unknown field", lambda d: d.update(unexpected=1), "root: fields")
    case("missing pack version", lambda d: d.pop("packVersion"), "root: fields")
    case("blank pack version", lambda d: d.update(packVersion=" "), "packVersion: expected nonblank string")
    case("invalid pack version type", lambda d: d.update(packVersion=True), "packVersion: expected nonblank string")
    case("unsupported pack version", lambda d: d.update(packVersion="snake-levels-v1-draft-other"), "packVersion: unsupported level pack")
    case("null defaults", lambda d: d.update(defaults=None), "defaults: expected object")
    case("boolean coordinate", lambda d: d["defaults"]["initialSnake"][0].__setitem__(0, True), "expected integer")
    case("duplicate snake", lambda d: d["defaults"]["initialSnake"].__setitem__(3, [4, 20]), "duplicate coordinates")
    case("discontinuous snake", lambda d: d["defaults"]["initialSnake"].__setitem__(3, [2, 20]), "orthogonally adjacent")
    case("duplicate id", lambda d: d["levels"][2].update(id="snake-02"), "duplicate or unexpected order")
    case("boolean tick", lambda d: d["levels"][2].update(tickMs=True), "expected integer")
    case("invalid boundary", lambda d: d["levels"][2].update(boundary="teleport"), "expected wrap or solid")
    case("duplicate obstacle", lambda d: d["levels"][2]["obstacles"].append([8, 8]), "duplicate coordinates")
    case("out of bounds", lambda d: d["levels"][2]["obstacles"].append([20, 8]), "expected 0..19")
    case("null obstacle", lambda d: d["levels"][2]["obstacles"].append(None), "expected [x, y]")
    case("snake collision", lambda d: d["levels"][2]["obstacles"].append([6, 20]), "overlaps initial snake")
    case("unsafe start", lambda d: d["levels"][2]["obstacles"].append([9, 20]), "step 3 is blocked")
    case("disconnected board", lambda d: d["levels"][2].update(obstacles=[[10, y] for y in range(24)]), "disconnected empty cells")
    case("static dead end", lambda d: d["levels"][2].update(obstacles=[[1, 0]]), "static dead end")
    case("one-cell corridor", lambda d: d["levels"][2].update(
        obstacles=[[10, y] for y in range(24) if y != 12]), "cells outside open 2x2 areas")
    case("one-cell bottleneck", lambda d: d["levels"][2].update(
        obstacles=[[x, y] for y in range(24) for x in range(20)
                   if not ((x <= 10 and y >= 12) or (x >= 10 and y <= 12))]), "one-cell static bottlenecks")
    case("target consumes capacity", lambda d: d["levels"][2].update(
        obstacles=[[x, y] for y in range(24) for x in range(20) if not (3 <= x <= 10 and 20 <= y <= 21)]),
        "strictly below free growth capacity 12")
    for name, data, expected in cases:
        try:
            validate(data)
        except ValidationError as error:
            require(expected in str(error), f"self-test {name}: unexpected failure: {error}")
        else:
            raise ValidationError(f"self-test {name}: invalid input was accepted")
    wrap_neighbors = set(adjacent((19, 6), 20, 24, "wrap"))
    require((0, 6) in wrap_neighbors, "self-test: right edge must wrap to x=0")
    require((19, 6) in set(adjacent((0, 6), 20, 24, "wrap")), "self-test: left edge must wrap to x=19")
    require((6, 0) in set(adjacent((6, 23), 20, 24, "wrap")), "self-test: bottom edge must wrap to y=0")
    require((6, 23) in set(adjacent((6, 0), 20, 24, "wrap")), "self-test: top edge must wrap to y=23")
    require((0, 6) not in set(adjacent((19, 6), 20, 24, "solid")), "self-test: solid edge must not wrap")
    require([0, 6] in valid_data["levels"][3]["obstacles"], "self-test: wrap landing obstacle fixture is missing")
    print(f"SELF-TEST PASS: {len(cases)} rejection cases; four-way wrap, solid edge, and blocked wrap landing fixture checked.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", nargs="?", type=Path, default=Path(__file__).with_name("levels-v1.draft.json"))
    parser.add_argument("--self-test", action="store_true", help="also check rejection cases and boundary helpers")
    args = parser.parse_args()
    try:
        with args.path.open(encoding="utf-8") as source:
            data = json.load(source)
        stats = validate(data)
        print(f"packVersion: {data['packVersion']}")
        print("id        boundary tickMs target obstacles free growth reachable minDegree cutCells")
        for s in stats:
            print(f"{s['id']:<9} {s['boundary']:<8} {s['tickMs']:>6} {s['targetFood']:>6} {s['obstacles']:>9} "
                  f"{s['freeCells']:>4} {s['growthCapacity']:>6} {s['reachable']:>9} {s['minDegree']:>9} {s['articulationPoints']:>8}")
        if args.self_test:
            self_test(data)
        print(f"PASS: {len(stats)} draft levels; all free cells connected, no static degree < 2, all covered by open 2x2 squares, no one-cell bottlenecks.")
        print("Scope: data/schema and static geometry only; no gameplay, random-food solvability, full-board route, or device verification.")
    except (OSError, json.JSONDecodeError, ValidationError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

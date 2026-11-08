#!/usr/bin/env python3
"""Rasterize the original Snake vector shapes in the lobby's paper/green palette."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SCALE = 8
GREEN = '#276653'
BODY = '#57916B'
PAPER = '#F6F4EC'


def canvas(size):
    image = Image.new('RGBA', (size * SCALE, size * SCALE), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image)


def box(points):
    return tuple(round(v * SCALE) for v in points)


def circle(draw, x, y, radius, color):
    draw.ellipse(box((x-radius, y-radius, x+radius, y+radius)), fill=color)


def stroke(draw, points, width, color):
    draw.line([(round(x*SCALE), round(y*SCALE)) for x, y in points], fill=color,
              width=round(width*SCALE), joint='curve')
    for x, y in points:
        circle(draw, x, y, width/2, color)


def bezier(a, b, c, d):
    return [((1-t)**3*a[0]+3*(1-t)**2*t*b[0]+3*(1-t)*t*t*c[0]+t**3*d[0],
             (1-t)**3*a[1]+3*(1-t)**2*t*b[1]+3*(1-t)*t*t*c[1]+t**3*d[1])
            for t in (i/32 for i in range(33))]


def save(image, path, size):
    image.resize((size,size), Image.Resampling.LANCZOS).save(ROOT/path)


def main():
    image, draw = canvas(24)
    path = [(4,20),(15,20)] + bezier((15,20),(22,20),(22,12),(15,12))
    path += [(9,12)] + bezier((9,12),(3,12),(3,5),(9,5)) + [(13,5)]
    stroke(draw,path,2,GREEN)
    draw.rounded_rectangle(box((11,2.5,17.5,7.5)), radius=2*SCALE, outline=GREEN, width=2*SCALE)
    circle(draw,15,4.5,.65,GREEN)
    circle(draw,21,5,1.5,GREEN)
    save(image,'Assets/Resources/Lobby/Icons/snake.png',128)

    image, draw = canvas(96)
    draw.rounded_rectangle(box((0,0,96,96)), radius=18*SCALE, fill='#DFE8DE')
    # The head and continuous winding body mirror the small outline icon.
    path=[(21,72),(59,72)] + bezier((59,72),(83,72),(83,45),(59,45))
    path += [(35,45)] + bezier((35,45),(20,45),(20,25),(35,25)) + [(43,25)]
    stroke(draw,path,14,BODY)
    draw.rounded_rectangle(box((37,15,59,35)),radius=7*SCALE,fill=GREEN)
    circle(draw,52,21.5,2.2,PAPER)
    circle(draw,52,28.5,2.2,PAPER)
    draw.rounded_rectangle(box((69,16,83,29)),radius=5*SCALE,fill='#C75D43')
    stroke(draw,[(76,16),(77,11)],1.8,GREEN)
    draw.ellipse(box((77,10,84,14)),fill=GREEN)
    save(image,'Assets/Resources/Snake/Logo.png',384)


if __name__ == '__main__':
    main()

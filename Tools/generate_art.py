"""Recreate original ArmOrb vector-style bitmap assets. Optional dependency: Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw
import math

ROOT = Path(__file__).resolve().parents[1] / 'Assets/ArmOrb/Art'
ROOT.mkdir(parents=True, exist_ok=True)
S = 3
INK = '#292d39'

def canvas(w, h):
    im = Image.new('RGBA', (w*S, h*S))
    return im, ImageDraw.Draw(im)

def poly(d, points, fill, width=3):
    points = [(int(x*S), int(y*S)) for x,y in points]
    d.polygon(points, fill=fill)
    d.line(points+[points[0]], fill=INK, width=width*S, joint='curve')

def line(d, points, fill, width):
    d.line([(int(x*S),int(y*S)) for x,y in points], fill=fill, width=width*S, joint='curve')

def save(im, name):
    im.resize((im.width//S, im.height//S), Image.Resampling.LANCZOS).save(ROOT / (name+'.png'))

im,d=canvas(128,128)
d.ellipse((4*S,4*S,124*S,124*S),fill=INK)
d.ellipse((8*S,8*S,120*S,120*S),fill='white')
d.ellipse((25*S,18*S,55*S,34*S),fill=(255,255,255,100))
save(im,'Orb')
im,d=canvas(128,128)
d.ellipse((4*S,4*S,124*S,124*S),fill='white')
save(im,'Disc')

for name in ['Sword','Spear','Blade','Scythe','Shield']:
    im,d=canvas(256,160)
    if name=='Sword':
        poly(d,[(42,69),(201,63),(253,80),(201,97),(42,91)],'#f5efbd')
        line(d,[(58,79),(233,79)],'#c3b974',3)
        poly(d,[(31,50),(48,50),(52,110),(35,110)],'#e8ad3e')
        poly(d,[(3,72),(32,72),(32,88),(3,88)],'#817060')
    elif name=='Spear':
        line(d,[(2,80),(202,80)],INK,13)
        line(d,[(2,78),(202,78)],'#ab7c60',7)
        for x in range(10,195,16): line(d,[(x,74),(x+6,84)],'#563f3f',3)
        poly(d,[(186,64),(253,80),(186,96),(196,80)],'#e3f0f0')
        line(d,[(202,80),(241,80)],'#8bafb8',2)
    elif name=='Blade':
        poly(d,[(27,72),(188,58),(253,80),(181,100),(44,93),(79,82)],'#bcd9f3')
        poly(d,[(31,51),(48,54),(48,106),(32,109)],'#dcb36b')
        poly(d,[(2,74),(31,74),(31,87),(2,87)],'#5d6c86')
        line(d,[(64,78),(225,79)],'#f4fcff',3)
    elif name=='Scythe':
        # Long grip, inward-curving hooked blade.
        line(d,[(3,80),(217,80)],INK,14)
        line(d,[(3,78),(217,78)],'#85705b',8)
        poly(d,[(207,82),(219,79),(225,42),(205,15),(164,2),(178,28),(200,43)],'#c4d5a0')
        line(d,[(216,75),(214,42),(191,18)],'#f0f5d7',3)
    else:
        poly(d,[(40,13),(160,4),(224,36),(224,110),(158,154),(40,128)],'#d2c2e3')
        poly(d,[(59,28),(155,21),(204,46),(204,100),(155,132),(59,114)],'#eee4f7')
        line(d,[(131,34),(131,119)],'#b79bcf',8)
        line(d,[(81,77),(185,77)],'#b79bcf',8)
        d.ellipse((115*S,61*S,147*S,93*S), fill='#f3c56a', outline=INK, width=3*S)
    save(im,name)

im=Image.open(ROOT/'Spear.png')
im.crop((186,0,256,160)).save(ROOT/'SpearHead.png')
im.crop((0,65,187,95)).save(ROOT/'SpearShaft.png')

im,d=canvas(128,128)
d.rounded_rectangle((0,0,128*S,128*S),radius=28*S,fill='#f7f2e9')
d.ellipse((12*S,42*S,72*S,102*S),fill='#f06565',outline=INK,width=4*S)
d.ellipse((63*S,19*S,115*S,71*S),fill='#1db9be',outline=INK,width=4*S)
poly(d,[(43,35),(76,11),(87,12),(82,24),(49,49)],'#f4cf70',2)
line(d,[(38,34),(53,51)],INK,4)
save(im,'AppIcon')

# A quiet illustration for the setup screen, drawn from the same weapon assets.
im,d=canvas(660,205)
for x,y,r in [(62,47,3),(610,157,4),(334,32,3),(313,173,2)]:
    d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill='#d9d2c6')
for x,y,r,c in [(175,117,43,'#f06565'),(487,101,43,'#1db9be')]:
    d.ellipse(((x-r)*S,(y-r+9)*S,(x+r)*S,(y+r+9)*S),fill=(41,45,57,18))
    d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=c,outline=INK,width=3*S)
for name,xy,rot in [('Sword',(201,52),30),('Spear',(340,100),-15)]:
    w=Image.open(ROOT/(name+'.png')).resize((150*S,94*S))
    w=w.rotate(rot,expand=True,resample=Image.Resampling.BICUBIC)
    im.alpha_composite(w,(xy[0]*S,xy[1]*S))
line(d,[(310,77),(330,97),(310,117)],'#b9b0a2',3)
line(d,[(336,77),(356,97),(336,117)],'#b9b0a2',3)
save(im,'SetupIllustration')
print('Generated',len(list(ROOT.glob('*.png'))),'original assets in',ROOT)

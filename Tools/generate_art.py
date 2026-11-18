"""Original low-resolution ArmOrb sprites. Optional build-time dependency: Pillow.

Weapon PNGs deliberately have hard alpha and no supersampling. Unity's scene builder
extracts the SAME opaque pixels into collision masks; no separately drawn hitboxes.
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]/'Assets/ArmOrb/Art'
ROOT.mkdir(parents=True,exist_ok=True)
INK='#111111'

def canvas(w,h):
    im=Image.new('RGBA',(w,h)); return im,ImageDraw.Draw(im)
def polygon(d,points,fill):
    d.polygon(points,fill=fill,outline=INK,width=2)
def save(im,name): im.save(ROOT/(name+'.png'))

im,d=canvas(128,128)
d.ellipse((0,0,127,127),fill=INK)
d.ellipse((4,4,123,123),fill='white')
save(im,'Orb')
im,d=canvas(128,128);d.ellipse((0,0,127,127),fill='white');save(im,'Disc')

im,d=canvas(64,32)
polygon(d,[(14,11),(49,5),(63,13),(63,18),(49,26),(14,21)],'#fcfcf2')
d.line([(21,16),(56,15)],fill='#bbbbc0',width=2)
polygon(d,[(12,6),(18,6),(18,27),(12,27)],'#f7d24e')
polygon(d,[(2,13),(12,13),(12,20),(2,20)],'#7e4741')
save(im,'Sword')

im,d=canvas(64,32)
polygon(d,[(14,13),(39,9),(50,3),(63,10),(57,18),(40,25),(20,22)],'#ecf6ff')
d.line([(24,18),(54,12)],fill='#90c5de',width=2)
polygon(d,[(12,7),(17,7),(20,26),(15,27)],'#bdc5d7')
polygon(d,[(2,14),(12,14),(14,20),(2,20)],'#574078')
save(im,'Blade')

im,d=canvas(8,8)
d.rectangle((0,2,7,5),fill=INK)
d.line([(0,3),(7,3)],fill='#a8787d',width=1)
d.point((2,4),fill='#623f49');d.point((6,4),fill='#623f49')
save(im,'SpearShaft')
im,d=canvas(24,24)
polygon(d,[(0,8),(8,8),(23,11),(23,13),(8,17),(0,16),(5,12)],'#f4f7ff')
d.line([(9,12),(19,12)],fill='#a4a3b6',width=1)
save(im,'SpearHead')
im,d=canvas(64,32)
d.line([(1,16),(45,16)],fill=INK,width=5)
d.line([(1,15),(45,15)],fill='#996d76',width=2)
for x in range(4,44,5):d.line([(x,14),(x+2,17)],fill='#4e3444',width=1)
head=Image.open(ROOT/'SpearHead.png'); im.alpha_composite(head,(40,4));save(im,'Spear')

im,d=canvas(64,64)
d.line([(1,32),(56,32)],fill=INK,width=5)
d.line([(1,31),(55,31)],fill='#756156',width=2)
polygon(d,[(52,33),(60,31),(63,17),(57,6),(44,0),(29,2),(38,8),(47,17),(50,25)],'#e3eac6')
d.line([(56,27),(58,17),(51,8),(40,4)],fill='#afbba1',width=2)
save(im,'Scythe')

im,d=canvas(32,40)
polygon(d,[(5,4),(24,0),(31,7),(31,29),(20,39),(5,33),(0,20)],'#ddd9ec')
polygon(d,[(9,9),(22,6),(26,11),(26,26),(19,32),(9,28),(5,19)],'#faf8ff')
d.line([(16,10),(16,29)],fill='#9e91b6',width=3)
d.line([(8,19),(24,19)],fill='#9e91b6',width=3)
d.rectangle((14,17,18,21),fill='#e5be43')
save(im,'Shield')

im,d=canvas(128,128);d.rectangle((0,0,127,127),fill='white')
d.ellipse((9,47,75,113),fill='#ff626a',outline=INK,width=4)
d.ellipse((65,13,119,67),fill='#00d3dc',outline=INK,width=4)
sword=Image.open(ROOT/'Sword.png').resize((64,32),Image.Resampling.NEAREST).rotate(40,expand=True,resample=Image.Resampling.NEAREST)
im.alpha_composite(sword,(23,5));save(im,'AppIcon')
print('Generated pixel sprites; no antialiasing on weapon masks.')

# v0.3: original pixel artwork, in the same scale and ink outline as the melee set.
im,d=canvas(40,40)
d.ellipse((2,2,37,37),fill='#fafafa',outline=INK,width=2)
d.line([(12,20),(17,25),(28,13)],fill='#b1b1b1',width=2)
save(im,'Unarmed')
im,d=canvas(48,48)
polygon(d,[(10,2),(20,5),(32,15),(36,24),(32,33),(20,43),(10,46),(16,36),(23,29),(25,24),(23,19),(16,12)],'#a87040')
d.line([(10,3),(10,44)],fill='#b8b9c4',width=1)
d.line([(3,24),(43,24)],fill=INK,width=2)
polygon(d,[(37,20),(47,24),(37,28)],'#e5e9ee')
save(im,'Bow')
im,d=canvas(64,32)
polygon(d,[(2,13),(45,13),(45,19),(2,19)],'#806044')
d.line([(4,14),(42,14)],fill='#ba9058',width=1)
polygon(d,[(43,9),(51,3),(61,7),(63,16),(58,25),(49,29),(43,23)],'#e6b23d')
polygon(d,[(48,13),(52,7),(58,11),(58,20),(52,24),(47,19)],'#ff653b')
d.rectangle((51,12,54,18),fill='#ffed94')
save(im,'Staff')
im,d=canvas(32,32)
polygon(d,[(15,1),(19,10),(30,7),(23,17),(30,29),(18,24),(9,31),(9,21),(1,14),(11,12)],'#c1cbd6')
d.ellipse((12,12,19,19),fill=INK);d.ellipse((14,14,17,17),fill='white')
save(im,'Shuriken')
im,d=canvas(40,12)
d.line([(2,6),(32,6)],fill=INK,width=3)
d.line([(5,5),(31,5)],fill='#ac7947',width=1)
polygon(d,[(30,1),(39,6),(30,11)],'#e3edf4')
d.line([(1,2),(8,6),(1,10)],fill='#b5b3bc',width=2)
save(im,'Arrow')
im,d=canvas(24,24)
polygon(d,[(0,12),(8,5),(11,1),(17,4),(23,12),(21,19),(13,23),(5,20)],'#ed5b31')
d.ellipse((7,7,20,20),fill='#ffaf38');d.ellipse((11,11,18,18),fill='#fff09b')
save(im,'Fireball')

im,d=canvas(64,48)
polygon(d,[(3,20),(52,20),(63,24),(52,28),(3,28)],'#92623e')
polygon(d,[(28,3),(37,2),(44,12),(46,24),(44,36),(37,46),(28,45),(34,34),(36,24),(34,14)],'#687784')
d.line([(28,4),(28,43)],fill='#cbc8ba',width=2)
d.line([(14,23),(60,23)],fill=INK,width=2)
polygon(d,[(55,18),(63,23),(55,28)],'#e0e6eb')
save(im,'Crossbow')

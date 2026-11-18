#!/usr/bin/env python3
"""Smoke-test the DEVELOPMENT APK on an explicitly selected, connected Android device."""
import argparse
from pathlib import Path
import subprocess
import time

PACKAGE = 'com.enotikpoker.armorb'
parser = argparse.ArgumentParser()
parser.add_argument('--adb', default='adb')
parser.add_argument('--serial', required=True, help='Device serial from adb devices')
parser.add_argument('--apk', type=Path)
parser.add_argument('--seconds', type=float, default=8)
parser.add_argument('--all-pairs', action='store_true', help='Opt in to 45 launches, restarting the app for each matchup')
parser.add_argument('--output', type=Path, default=Path('TestResults/device'))
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

def adb(*command, binary=False):
    result = subprocess.run([args.adb, '-s', args.serial, *map(str, command)], capture_output=True, check=True)
    return result.stdout if binary else result.stdout.decode(errors='replace').strip()

if args.apk:
    print(adb('install', '-r', args.apk), flush=True)

pairs = [(a,b) for a in range(9) for b in range(a,9)] if args.all_pairs else [(0,1)]
names = ['Sword','Spear','Blade','Scythe','Shield','Unarmed','Bow','Staff','Shuriken']
failures = []
component = adb('shell','cmd','package','resolve-activity','--brief','-p',PACKAGE).splitlines()[-1]
if '/' not in component: raise SystemExit('Cannot resolve installed ArmOrb activity: '+component)
for a,b in pairs:
    name = f'{names[a]}-{names[b]}'
    # Intent extras are accepted only by development builds.
    config = f'{a},{b},100,100,104729'
    output = adb('shell','am','start','-S','-n',component,'--es','armorb.qa',config)
    time.sleep(args.seconds)
    pid = adb('shell','pidof',PACKAGE)
    log = adb('logcat','-d',f'--pid={pid}','-v','brief')
    (args.output / f'{name}.log').write_text(log)
    (args.output / f'{name}.png').write_bytes(adb('exec-out','screencap','-p',binary=True))
    failed = any(x in log for x in ['FATAL EXCEPTION','NullReferenceException','MissingReferenceException','DllNotFoundException'])
    failed |= f'ArmOrb START {names[a]} vs {names[b]}' not in log
    if failed: failures.append(name)
    print(f'{name}: {"FAIL" if failed else "PASS"}, pid={pid}', flush=True)

if failures: raise SystemExit('Failed: '+', '.join(failures))
print(f'Passed {len(pairs)} device launches; logs and screenshots in {args.output}', flush=True)

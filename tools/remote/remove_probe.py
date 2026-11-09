#!/usr/bin/env python3
import os, re, sys, time, shutil
B = "/home/esteraone/esteraone.green/bestlaptop-erp-be"
TS = time.strftime("%Y%m%d-%H%M%S")

def backup(p):
    d = f"{p}.bak-noprobe-{TS}"
    if not os.path.exists(d): shutil.copy2(p, d)

def strip(path, imp_old, imp_new):
    src = open(path, encoding="utf-8").read()
    if "TEMP-PROBE" not in src:
        print("SKIP (no probe):", path); return
    backup(path)
    # hapus blok probe (komentar + this.app.get(...) sampai penutup '});' + baris kosong)
    src = re.sub(r'\n *// TEMP-PROBE 20261026:.*?\n *\}\);\n\n', '\n', src, count=1, flags=re.S)
    src = src.replace(imp_old, imp_new, 1)
    open(path, "w", encoding="utf-8").write(src)
    print("CLEANED:", path, "| probe left:", "TEMP-PROBE" in src)

strip(os.path.join(B, "dist/app.js"),
      'import { dbScope, prisma, activeDb } from "./db.js";',
      'import { dbScope } from "./db.js";')
strip(os.path.join(B, "src/app.ts"),
      'import { dbScope, prisma, activeDb } from "./db.js";',
      'import { dbScope } from "./db.js";')
print("DONE")

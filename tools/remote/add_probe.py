#!/usr/bin/env python3
import os, sys, time, shutil, re
B = "/home/esteraone/esteraone.green/bestlaptop-erp-be"
TS = time.strftime("%Y%m%d-%H%M%S")

PROBE_JS = '''        // TEMP-PROBE 20261026: bukti runtime DB per host (hapus setelah verifikasi)
        this.app.get("/api/_dbprobe", async (req, res) => {
            try {
                const rows = await prisma.$queryRawUnsafe("select current_database() as db, inet_server_port() as port");
                res.json({ host: req.headers.host || null, scope: activeDb(), db: rows?.[0]?.db, port: rows?.[0]?.port });
            } catch (e) {
                res.status(500).json({ host: req.headers.host || null, scope: activeDb(), error: e.message });
            }
        });

'''
PROBE_TS = '''        // TEMP-PROBE 20261026: bukti runtime DB per host (hapus setelah verifikasi)
        this.app.get("/api/_dbprobe", async (req: express.Request, res: express.Response) => {
            try {
                const rows: any = await prisma.$queryRawUnsafe("select current_database() as db, inet_server_port() as port");
                res.json({ host: req.headers.host || null, scope: activeDb(), db: rows?.[0]?.db, port: rows?.[0]?.port });
            } catch (e: any) {
                res.status(500).json({ host: req.headers.host || null, scope: activeDb(), error: e.message });
            }
        });

'''

def backup(p):
    d = f"{p}.bak-probe-{TS}"
    if not os.path.exists(d): shutil.copy2(p, d)

def patch(path, probe, import_anchor, method_anchor, new_imports):
    src = open(path, encoding="utf-8").read()
    if "TEMP-PROBE" in src:
        print("SKIP (probe already present):", path); return
    for a in (import_anchor, method_anchor):
        if a not in src:
            print("FAIL anchor:", a, "in", path); sys.exit(1)
    backup(path)
    src = src.replace(import_anchor, new_imports, 1)
    src = src.replace(method_anchor, method_anchor + "\n" + probe, 1)
    open(path, "w", encoding="utf-8").write(src)
    print("PATCHED:", path)

patch(os.path.join(B, "dist/app.js"), PROBE_JS,
      'import { dbScope } from "./db.js";',
      "    initRoutes() {",
      'import { dbScope, prisma, activeDb } from "./db.js";')

patch(os.path.join(B, "src/app.ts"), PROBE_TS,
      'import { dbScope } from "./db.js";',
      "    private initRoutes() {",
      'import { dbScope, prisma, activeDb } from "./db.js";')

print("DONE")

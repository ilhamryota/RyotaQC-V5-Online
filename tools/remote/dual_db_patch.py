#!/usr/bin/env python3
import os, re, sys, time, shutil

B = "/home/esteraone/esteraone.green/bestlaptop-erp-be"
TS = time.strftime("%Y%m%d-%H%M%S")
MARK = "DUALDB-PATCH"

DB_JS_NEW = '''import { PrismaClient } from "@prisma/client";
import { AsyncLocalStorage } from "node:async_hooks";
// DUALDB-PATCH 20261026: satu proses backend (port 8002), dua database.
// Host dev.*  -> DATABASE_URL_DEV  (5439 / esteraone_dev)
// Selain itu -> DATABASE_URL      (5438 / esteraone, PRODUKSI)
const _urlProd = process.env.DATABASE_URL_PROD || process.env.DATABASE_URL;
const _urlDev = process.env.DATABASE_URL_DEV || process.env.DATABASE_URL;
console.log("DB_URL_PROD", _urlProd);
console.log("DB_URL_DEV", _urlDev);
const _sameDb = _urlProd === _urlDev;
const _clientProd = new PrismaClient(_sameDb ? {} : { datasourceUrl: _urlProd });
const _clientDev = _sameDb ? _clientProd : new PrismaClient({ datasourceUrl: _urlDev });
export const dbScope = new AsyncLocalStorage();
export const activeDb = () => (dbScope.getStore() === "dev" ? "dev" : "prod");
const _pick = () => (dbScope.getStore() === "dev" ? _clientDev : _clientProd);
export const prisma = new Proxy({}, {
    get(_t, prop) {
        const c = _pick();
        const v = c[prop];
        return typeof v === "function" ? v.bind(c) : v;
    },
    has(_t, prop) { return prop in _pick(); }
});
'''

DB_TS_NEW = '''import { PrismaClient } from "@prisma/client";
import { AsyncLocalStorage } from "node:async_hooks";
// DUALDB-PATCH 20261026: satu proses backend (port 8002), dua database.
// Host dev.*  -> DATABASE_URL_DEV  (5439 / esteraone_dev)
// Selain itu -> DATABASE_URL      (5438 / esteraone, PRODUKSI)
type DbScopeValue = "prod" | "dev";
const _urlProd = process.env.DATABASE_URL_PROD || process.env.DATABASE_URL;
const _urlDev = process.env.DATABASE_URL_DEV || process.env.DATABASE_URL;
console.log("DB_URL_PROD", _urlProd);
console.log("DB_URL_DEV", _urlDev);
const _sameDb = _urlProd === _urlDev;
const _clientProd = new PrismaClient(_sameDb ? {} : { datasourceUrl: _urlProd as string });
const _clientDev = _sameDb ? _clientProd : new PrismaClient({ datasourceUrl: _urlDev as string });
export const dbScope = new AsyncLocalStorage<DbScopeValue>();
export const activeDb = () => (dbScope.getStore() === "dev" ? "dev" : "prod");
const _pick = () => (dbScope.getStore() === "dev" ? _clientDev : _clientProd);
export const prisma = new Proxy({} as PrismaClient, {
    get(_t, prop) {
        const c = _pick();
        const v = (c as any)[prop];
        return typeof v === "function" ? v.bind(c) : v;
    },
    has(_t, prop) { return prop in _pick(); },
}) as PrismaClient;
'''

MW_JS = '''        // DUALDB-PATCH: pilih database per host (prod 5438 / dev 5439)
        this.app.use((req, _res, next) => {
            const host = String(req.headers["x-forwarded-host"] || req.headers.host || "").toLowerCase();
            dbScope.run(host.startsWith("dev.") ? "dev" : "prod", () => next());
        });
'''

MW_TS = '''        // DUALDB-PATCH: pilih database per host (prod 5438 / dev 5439)
        this.app.use((req: express.Request, _res: express.Response, next: express.NextFunction) => {
            const host = String(req.headers["x-forwarded-host"] || req.headers.host || "").toLowerCase();
            dbScope.run(host.startsWith("dev.") ? "dev" : "prod", () => next());
        });
'''


def backup(path):
    dst = f"{path}.bak-dualdb-{TS}"
    if not os.path.exists(dst):
        shutil.copy2(path, dst)
    return dst


def patch_db(path, new_block, marker_import):
    src = open(path, encoding="utf-8").read()
    if MARK in src:
        print("SKIP (already patched):", path)
        return
    pat = re.compile(r'import \{ PrismaClient \} from "@prisma/client";.*?globalForPrisma\.prisma = prisma;', re.S)
    if not pat.search(src):
        print("FAIL: header block not found in", path)
        sys.exit(1)
    backup(path)
    src = pat.sub(lambda m: new_block.rstrip("\n"), src, count=1)
    open(path, "w", encoding="utf-8").write(src)
    print("PATCHED db:", path)


def patch_app(path, marker_import, mw, anchor_import, anchor_method):
    src = open(path, encoding="utf-8").read()
    if MARK in src:
        print("SKIP (already patched):", path)
        return
    if anchor_import not in src:
        print("FAIL: import anchor not found in", path)
        sys.exit(1)
    if anchor_method not in src:
        print("FAIL: method anchor not found in", path)
        sys.exit(1)
    backup(path)
    src = src.replace(anchor_import, anchor_import + '\nimport { dbScope } from "./db.js";', 1)
    src = src.replace(anchor_method, anchor_method + "\n" + mw, 1)
    open(path, "w", encoding="utf-8").write(src)
    print("PATCHED app:", path)


# --- dist (yang benar-benar dijalankan) ---
patch_db(os.path.join(B, "dist/db.js"), DB_JS_NEW, None)
patch_app(
    os.path.join(B, "dist/app.js"),
    None,
    MW_JS,
    'import { kpiModule } from "./modules/kpi/kpi.module.js";',
    "    initMiddlewares() {",
)

# --- src (sumber, biar build berikutnya tidak revert) ---
patch_db(os.path.join(B, "src/db.ts"), DB_TS_NEW, None)
patch_app(
    os.path.join(B, "src/app.ts"),
    None,
    MW_TS,
    'import { kpiModule } from "./modules/kpi/kpi.module.js";',
    "    private initMiddlewares() {",
)

print("DONE")

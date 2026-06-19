/**
 * Unity `MasterData` 取得契約の互換実装。
 * 正本仕様: spec/code/Remote/data-studio.md
 *
 * Unity 側 (MasterData.cs) は次を期待する:
 *   GET {MasterDataAPIURI}            -> MasterDataVersion  { TimeStamp, <sheet>: <version>, ... }
 *   GET {MasterDataAPIURI}?sheet=X    -> SpreadSheetDataObject 派生 { Version, Data: [...] }
 *
 * サーバはこれを /master に実装し、GameSettings.MasterDataAPIURI を /master に
 * 向けるだけで Web 製データを Unity が取り込めるようにする。
 */
import type { SheetStore } from "./store.js";

export interface MasterVersionDoc {
  /** Unity は DateTime.Now.Ticks と比較する(86400 ticks 窓)。.NET ticks(100ns)で出す。 */
  TimeStamp: number;
  [sheet: string]: number;
}

export interface MasterSheetDoc {
  Version: number;
  Data: Record<string, unknown>[];
}

/** Unix epoch ms から .NET DateTime.Ticks(100ns 単位)へ。 */
export function toDotNetTicks(epochMs: number): number {
  // .NET epoch = 0001-01-01。Unix epoch との差 = 621355968000000000 ticks。
  return 621355968000000000 + epochMs * 10000;
}

/** 全シートのバージョン表を構築する。 */
export async function buildVersionDoc(store: SheetStore, nowMs: number): Promise<MasterVersionDoc> {
  const names = await store.list();
  const doc: MasterVersionDoc = { TimeStamp: toDotNetTicks(nowMs) };
  for (const name of names) {
    const sheet = await store.get(name);
    if (sheet) doc[name] = sheet.schema.version;
  }
  return doc;
}

/** 1 シートを Unity 互換 JSON に変換する。存在しなければ null。 */
export async function buildSheetDoc(store: SheetStore, name: string): Promise<MasterSheetDoc | null> {
  const sheet = await store.get(name);
  if (!sheet) return null;
  return { Version: sheet.schema.version, Data: sheet.rows };
}

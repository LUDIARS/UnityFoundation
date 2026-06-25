/**
 * シートの永続化ストア。1 シート = 1 JSON ファイル (data/sheets/<name>.json)。
 * 人手編集・git diff しやすいよう整形して書き出す。
 */
import { promises as fs } from "node:fs";
import path from "node:path";
import {
  isValidSheetName,
  normalizeRows,
  validateSchema,
  type Row,
  type Sheet,
  type SheetSchema,
} from "./schema.js";

interface StoredSheet {
  schema: SheetSchema;
  rows: Row[];
}

export class SheetStore {
  constructor(private readonly dir: string) {}

  private file(name: string): string {
    return path.join(this.dir, `${name}.json`);
  }

  async init(): Promise<void> {
    await fs.mkdir(this.dir, { recursive: true });
  }

  /** 全シート名を列挙する。 */
  async list(): Promise<string[]> {
    try {
      const files = await fs.readdir(this.dir);
      return files
        .filter((f) => f.endsWith(".json"))
        .map((f) => f.slice(0, -5))
        .filter(isValidSheetName)
        .sort();
    } catch {
      return [];
    }
  }

  /** 1 シートを読む。存在しなければ null。 */
  async get(name: string): Promise<Sheet | null> {
    if (!isValidSheetName(name)) return null;
    try {
      const raw = await fs.readFile(this.file(name), "utf8");
      const parsed = JSON.parse(raw) as StoredSheet;
      return { schema: parsed.schema, rows: parsed.rows ?? [] };
    } catch {
      return null;
    }
  }

  /** スキーマを upsert する(既存行はスキーマに合わせて正規化し維持)。 */
  async putSchema(schema: SheetSchema): Promise<Sheet> {
    validateSchema(schema);
    const existing = await this.get(schema.name);
    const rows = existing ? normalizeRows(schema, existing.rows) : [];
    const sheet: Sheet = { schema, rows };
    await this.write(sheet);
    return sheet;
  }

  /** 行を一括置換する(スキーマで正規化)。 */
  async putRows(name: string, rows: Row[]): Promise<Sheet> {
    const sheet = await this.get(name);
    if (!sheet) throw new Error(`シートが存在しません: ${name}`);
    sheet.rows = normalizeRows(sheet.schema, rows);
    await this.write(sheet);
    return sheet;
  }

  /** version をインクリメントして保存し、新 version を返す。 */
  async bumpVersion(name: string): Promise<number> {
    const sheet = await this.get(name);
    if (!sheet) throw new Error(`シートが存在しません: ${name}`);
    sheet.schema.version += 1;
    await this.write(sheet);
    return sheet.schema.version;
  }

  async remove(name: string): Promise<boolean> {
    if (!isValidSheetName(name)) return false;
    try {
      await fs.unlink(this.file(name));
      return true;
    } catch {
      return false;
    }
  }

  private async write(sheet: Sheet): Promise<void> {
    await fs.mkdir(this.dir, { recursive: true });
    await fs.writeFile(this.file(sheet.schema.name), JSON.stringify(sheet, null, 2) + "\n", "utf8");
  }
}

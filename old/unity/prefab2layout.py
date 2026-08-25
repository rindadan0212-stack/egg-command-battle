# -*- coding: utf-8 -*-
"""⚠️ 関門4 の試験用 probe（道具ではない）。

Prefab YAML を読んで骨組み（Layouts/*.txt）に変換できるかを見る。
⭐ 見たいのは「変換できた数」ではなく「**できなかったものは何か**」。
"""
import io, re, sys, os
sys.collections = None
sys.stdout.reconfigure(encoding="utf-8")

DIR = r"C:\Users\Indie\Desktop\gamedev\Egg Command Battle\unity\Assets\Resources\Prefabs"
W, H = 1080.0, 1920.0


def docs(text):
    """--- !u!<型> &<id> で区切られた塊に割る。"""
    out = []
    for m in re.finditer(r"--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)", text, re.S):
        out.append((int(m.group(1)), m.group(2), m.group(3)))
    return out


def num(body, key, default=None):
    m = re.search(rf"{key}: \{{x: ([-\d.eE]+), y: ([-\d.eE]+)", body)
    if m:
        return float(m.group(1)), float(m.group(2))
    return default


def parse(path):
    text = io.open(path, encoding="utf-8").read()
    objs, rects, comps = {}, {}, {}
    for kind, oid, body in docs(text):
        if kind == 1:  # GameObject
            name = re.search(r"m_Name: (.*)", body)
            active = re.search(r"m_IsActive: (\d)", body)
            parts = re.findall(r"component: \{fileID: (\d+)\}", body)
            objs[oid] = dict(name=(name.group(1).strip() if name else "?"),
                             active=(active.group(1) == "1" if active else True),
                             comps=parts)
        elif kind == 224:  # RectTransform
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            father = re.search(r"m_Father: \{fileID: (\d+)\}", body)
            kids = re.findall(r"- \{fileID: (\d+)\}", body.split("m_Children:")[1].split("m_Father:")[0]) \
                if "m_Children:" in body else []
            rects[oid] = dict(
                go=go.group(1) if go else None,
                father=father.group(1) if father else "0",
                kids=kids,
                anchorMin=num(body, "m_AnchorMin", (0, 0)),
                anchorMax=num(body, "m_AnchorMax", (0, 0)),
                pos=num(body, "m_AnchoredPosition", (0, 0)),
                size=num(body, "m_SizeDelta", (0, 0)),
                pivot=num(body, "m_Pivot", (0.5, 0.5)),
            )
        else:
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            script = re.search(r"m_Script: \{fileID: ([-\d]+), guid: ([0-9a-f]+)", body)
            if go:
                comps.setdefault(go.group(1), []).append(
                    dict(kind=kind, script=(script.group(2) if script else None), body=body))
    return objs, rects, comps


# Unity 組み込みの型番号
TEXT, IMAGE, BUTTON, SCROLL, MASK = 114, 114, 114, 114, 114
BUILTIN = {
    "1741964061": "text",     # ではなく guid で見るべきだが、型番号で近似する
}


def kind_of(oid, objs, comps):
    """部品の種類を当てる。⚠️ 当てられなければ None を返す（それが知りたい）。"""
    got = comps.get(oid, [])
    names = []
    for c in got:
        b = c["body"]
        if "m_FontData:" in b or "m_Text:" in b:
            names.append("label")
        elif "m_OnClick:" in b:
            names.append("button")
        elif "m_Content: {fileID:" in b and "m_Horizontal:" in b:
            names.append("scroll")
        elif "m_Sprite: {fileID:" in b:
            names.append("card")
    if "button" in names:
        return "button"
    if "scroll" in names:
        return "scroll"
    if "label" in names:
        return "label"
    if "card" in names:
        return "card"
    return "box"


def convert(path):
    objs, rects, comps = parse(path)
    roots = [oid for oid, r in rects.items() if r["father"] == "0"]
    lines, notes = [], []

    def walk(rid, depth, parentW, parentH):
        r = rects[rid]
        go = objs.get(r["go"], {})
        name = go.get("name", "?").replace(" ", "_") or "?"
        amin, amax = r["anchorMin"], r["anchorMax"]
        px, py = r["pos"]
        sw, sh = r["size"]
        pvx, pvy = r["pivot"]

        stretched = (amin != amax)
        if stretched:
            notes.append(f"{name}: 引き伸ばし（anchor {amin}→{amax}）── 絶対座標で書けない")
        # 左上原点へ写す（anchorMin==anchorMax の場合だけ正しい）
        ax, ay = amin
        left = parentW * ax + px - sw * pvx
        top = parentH * (1.0 - ay) - py - sh * (1.0 - pvy)
        k = kind_of(r["go"], objs, comps)
        if not go.get("active", True):
            notes.append(f"{name}: 既定で隠れている ── `when=` が要る")
        lines.append("  " * depth + f"{name} {k} {left:.0f} {top:.0f} {sw:.0f} {sh:.0f}")
        for kid in r["kids"]:
            if kid in rects:
                walk(kid, depth + 1, sw, sh)

    for rid in roots:
        walk(rid, 0, W, H)
    return lines, notes


for f in ["BoxScreen", "CreatureCell", "BreedScreen", "HomeScreen"]:
    path = os.path.join(DIR, f + ".prefab")
    lines, notes = convert(path)
    print(f"\n=== {f} ===  部品 {len(lines)} 個 / ⚠️ 書けない箇所 {len(notes)} 件")
    for l in lines[:14]:
        print("   " + l)
    if len(lines) > 14:
        print(f"   … 他 {len(lines)-14} 行")
    seen = set()
    for n in notes:
        key = n.split(":")[1].strip()[:24]
        if key in seen:
            continue
        seen.add(key)
        print("   ⚠️ " + n)

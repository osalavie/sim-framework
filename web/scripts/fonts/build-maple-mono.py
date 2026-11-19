#!/usr/bin/env python3
"""Regenerate the self-hosted Maple Mono CN webfont slices used across the UI.

Source: https://github.com/subframe7536/maple-font (SIL OFL 1.1)
The generated woff2 files under src/assets/fonts/maple-mono and
src/styles/maple-mono.css are committed; run this script only when the
upstream release is upgraded.

    python scripts/fonts/build-maple-mono.py --zip /tmp/MapleMono-CN.zip

Dependencies: pip install fonttools brotli wordfreq

Layout: for every shipped face (regular, bold, extra bold, italic) one Latin
slice (ASCII, symbols, arrows, box drawing, CJK punctuation, fullwidth, PUA)
plus GB 2312 level-1 Han characters split into frequency-ordered slices. Every
face declares an explicit unicode-range, so a page only downloads the Chinese
slices whose characters it actually renders. Han characters outside GB 2312
level 1 fall back to the next family in the CSS stack.
"""

import argparse
import os
import subprocess
import sys
import tempfile
import zipfile

from fontTools.ttLib import TTFont
from fontTools.ttLib.tables import ttProgram

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
WEB_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
ASSET_DIR = os.path.join(WEB_ROOT, 'src', 'assets', 'fonts', 'maple-mono')
CSS_PATH = os.path.join(WEB_ROOT, 'src', 'styles', 'maple-mono.css')

FAMILY = 'Maple Mono CN'
# (CSS font-weight, CSS font-style, release style, file label, 是否补 GB 2312 二级汉字)
# 正文与粗体承载绝大多数文字，补到二级可保证生僻字不回退系统字体；标题与斜体的
# 生僻字仍回退，避免再多出约 1.7 MB。
FACES = [
    ('400', 'normal', 'Regular', 'regular', True),
    ('700', 'normal', 'Bold', 'bold', True),
    ('900', 'normal', 'ExtraBold', 'black', False),
    ('400', 'italic', 'Italic', 'italic', False),
]
CHUNK_SIZE = 480
# Maple Mono CN 的汉字步进是 latin（600/1000 em）的两倍，字面四周留白让网页
# 正文的汉字间距明显偏大。这里把汉字与全角标点的步进收窄到 1.0 em 并把字形
# 左移一半保持居中，排印节奏与常规中文字体一致；中英宽度因此不再是严格 2:1。
CJK_ADVANCE_CUT = 200
# 空格（含不换行空格）按 0.3 em 出字，贴合常规排版词距；代码块在 CSS 里用
# word-spacing 补回 0.6 em，保证等宽空格仍占一个字符宽。
SPACE_ADVANCE = 300


def hanzi_level(level):
    """GB 2312 一级（B0-D7）或二级（D8-F7）汉字。"""
    first_range = range(0xB0, 0xD8) if level == 1 else range(0xD8, 0xF8)
    chars = set()
    for first in first_range:
        for second in range(0xA1, 0xFF):
            try:
                chars.add(bytes([first, second]).decode('gb2312'))
            except UnicodeDecodeError:
                continue
    return chars


def frequency_order(chars):
    """按使用频次排序；wordfreq 缺失时退回 GB 2312 编码顺序。"""
    try:
        from wordfreq import top_n_list
    except ImportError:
        print('wordfreq not installed, falling back to GB 2312 order', file=sys.stderr)
        return sorted(chars)
    ranked = []
    seen = set()
    for token in top_n_list('zh', 120000):
        if len(token) != 1 or token in seen:
            continue
        if 0x4E00 <= ord(token) <= 0x9FFF:
            seen.add(token)
            ranked.append(token)
    return [char for char in ranked if char in chars] + [
        char for char in sorted(chars) if char not in seen
    ]


def tighten_cjk_advance(font_path, cut):
    """收窄汉字与全角标点的步进宽度，并把字形左移一半以保持居中。"""
    font = TTFont(font_path)
    cmap = font.getBestCmap()
    glyf = font['glyf']
    hmtx = font['hmtx']

    names = set()
    for code, name in cmap.items():
        if 0x4E00 <= code <= 0x9FFF or 0x3000 <= code <= 0x303F or 0xFF00 <= code <= 0xFFEF:
            names.add(name)

    half = cut // 2
    for name in names:
        glyph = glyf[name]
        advance, lsb = hmtx[name]
        shift = 0
        if glyph.numberOfContours != 0:
            # 少数斜体汉字的字面已贴近字身左边缘，回移时不能越过原点
            shift = min(half, max(0, glyph.xMin))
            if glyph.isComposite():
                for component in glyph.components:
                    component.x -= shift
            else:
                glyph.coordinates.translate((-shift, 0))
                glyph.recalcBounds(glyf)
            if hasattr(glyph, 'program'):
                glyph.program = ttProgram.Program()
        hmtx[name] = (advance - cut, lsb - shift)

    font.save(font_path)


def narrow_space_advance(font_path, advance):
    """把空格与不换行空格的步进收窄到常规词距。"""
    font = TTFont(font_path)
    cmap = font.getBestCmap()
    hmtx = font['hmtx']

    names = {cmap[code] for code in (0x0020, 0x00A0) if code in cmap}
    for name in names:
        _, lsb = hmtx[name]
        hmtx[name] = (advance, lsb)

    font.save(font_path)


def latin_and_symbols(cmap):
    keep = set()
    for code in cmap:
        if (
            code < 0x2E80
            or 0x3000 <= code <= 0x303F
            or 0xFF00 <= code <= 0xFFEF
            or 0xE000 <= code <= 0xF8FF
        ):
            keep.add(code)
    return keep


def to_ranges(codes):
    ranges = []
    start = prev = None
    for code in sorted(codes):
        if start is None:
            start = prev = code
        elif code == prev + 1:
            prev = code
        else:
            ranges.append((start, prev))
            start = prev = code
    if start is not None:
        ranges.append((start, prev))
    return ', '.join(
        'U+%04X' % low if low == high else 'U+%04X-%04X' % (low, high)
        for low, high in ranges
    )


def write_unicodes(codes, path):
    with open(path, 'w', encoding='utf-8', newline='\n') as handle:
        handle.write('\n'.join('U+%04X' % code for code in sorted(codes)))


def subset(ttf_path, work_dir, name, codes, features, extra=()):
    out_path = os.path.join(ASSET_DIR, name)
    unicodes_path = os.path.join(work_dir, 'unicodes.txt')
    write_unicodes(codes, unicodes_path)
    command = [
        sys.executable,
        '-m',
        'fontTools.subset',
        ttf_path,
        '--unicodes-file=' + unicodes_path,
        '--flavor=woff2',
        '--layout-features=' + features,
        '--no-glyph-names',
        *extra,
        '--output-file=' + out_path,
    ]
    subprocess.run(command, check=True, stderr=subprocess.DEVNULL, stdout=subprocess.DEVNULL)
    return os.path.getsize(out_path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--zip', required=True, help='MapleMono-CN.zip from the maple-font release')
    args = parser.parse_args()

    os.makedirs(ASSET_DIR, exist_ok=True)
    with tempfile.TemporaryDirectory() as work_dir:
        with zipfile.ZipFile(args.zip) as archive:
            archive.extract('LICENSE.txt', work_dir)
            for _, _, source, _, _ in FACES:
                path = os.path.join(work_dir, 'MapleMono-CN-%s.ttf' % source)
                archive.extract('MapleMono-CN-%s.ttf' % source, work_dir)
                narrow_space_advance(path, SPACE_ADVANCE)
                tighten_cjk_advance(path, CJK_ADVANCE_CUT)

        reference = os.path.join(work_dir, 'MapleMono-CN-%s.ttf' % FACES[0][2])
        cmap = set(TTFont(reference).getBestCmap())

        latin = latin_and_symbols(cmap)
        ordered1 = [ord(char) for char in frequency_order(hanzi_level(1)) if ord(char) in cmap]
        ordered2 = [ord(char) for char in frequency_order(hanzi_level(2)) if ord(char) in cmap]

        faces = []
        for weight, style, source, label, with_level2 in FACES:
            ttf_path = os.path.join(work_dir, 'MapleMono-CN-%s.ttf' % source)
            face_cmap = set(TTFont(ttf_path).getBestCmap())

            face_latin = latin & face_cmap
            name = 'maple-mono-%s-latin.woff2' % label
            size = subset(ttf_path, work_dir, name, face_latin, '*')
            faces.append((weight, style, name, face_latin, size))

            ordered = ordered1 + (ordered2 if with_level2 else [])
            chunks = [
                ordered[index:index + CHUNK_SIZE]
                for index in range(0, len(ordered), CHUNK_SIZE)
            ]
            for index, chunk in enumerate(chunks, start=1):
                face_chunk = set(chunk) & face_cmap
                if not face_chunk:
                    continue
                name = 'maple-mono-%s-cjk-%02d.woff2' % (label, index)
                size = subset(ttf_path, work_dir, name, face_chunk, '', ('--no-hinting',))
                faces.append((weight, style, name, face_chunk, size))

        with open(os.path.join(work_dir, 'LICENSE.txt'), 'rb') as source:
            with open(os.path.join(ASSET_DIR, 'OFL.txt'), 'wb') as target:
                target.write(source.read())

    lines = [
        '/* Generated by scripts/fonts/build-maple-mono.py — do not edit by hand.',
        '   Maple Mono CN (https://github.com/subframe7536/maple-font), SIL OFL 1.1.',
        '   Latin and GB 2312 level-1 Han characters are split by unicode-range so the',
        '   Chinese slices only download when a page actually renders them. */',
        '',
    ]
    for weight, style, name, codes, size in faces:
        lines.extend([
            '@font-face {',
            "  font-family: '%s';" % FAMILY,
            '  font-style: %s;' % style,
            '  font-weight: %s;' % weight,
            '  font-display: swap;',
            "  src: url('../assets/fonts/maple-mono/%s') format('woff2');" % name,
            '  unicode-range: %s;' % to_ranges(codes),
            '}',
            '',
        ])
        print('%-34s %6.0f KB' % (name, size / 1024))

    with open(CSS_PATH, 'w', encoding='utf-8', newline='\n') as handle:
        handle.write('\n'.join(lines))
    total = sum(size for _, _, _, _, size in faces)
    print('total %.1f MB across %d faces -> %s' % (total / 1048576, len(faces), CSS_PATH))


if __name__ == '__main__':
    main()

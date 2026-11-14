"""从原始 Newsreader 可变字体和本机中文字体重建运行时字体，需安装 fontTools。

运行：python Tools/HueRelay/RebuildFonts.py
源字体与生成字体分目录存放，常规游戏运行不需要执行此工具。
"""
from argparse import ArgumentParser
from pathlib import Path
import os

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

PROJECT_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIRECTORY = PROJECT_ROOT / 'Assets/HueRelay/Resources/HueRelay/Fonts'
REFERENCE_DIRECTORY = PROJECT_ROOT / 'Reference/HueRelay'


def rebuild_newsreader(source_path: Path) -> None:
    """烘焙实际使用的字重、光学尺寸和 CSS 数字字距，不修改字形轮廓。"""
    styles = [('Score', 380, 44, 40), ('Big', 380, 68, 60), ('Float', 500, 32, 0)]
    for suffix, weight, optical_size, tracking in styles:
        font = TTFont(source_path)
        instantiateVariableFont(font, {'wght': weight, 'opsz': optical_size}, inplace=True)
        for name in font['name'].names:
            if name.nameID in (1, 4, 6):
                name.string = ('HueNewsreader' + suffix).encode(name.getEncoding())

        # Legacy UI Text 没有字距接口，因此将原 CSS 的负字距烘焙进数字 advance。
        if tracking:
            for digit in '0123456789':
                glyph = font.getBestCmap()[ord(digit)]
                advance, bearing = font['hmtx'][glyph]
                font['hmtx'][glyph] = (advance - tracking, bearing)
        font.save(OUTPUT_DIRECTORY / f'Newsreader{suffix}.ttf')
        font.close()


def subset_chinese(source_path: Path, output_name: str, characters: str) -> None:
    """保留 HTML 实际字符与基础 ASCII，避免把完整中文字库放入运行时资源。"""
    font = TTFont(source_path, fontNumber=0)
    options = subset.Options()
    options.name_IDs = ['*']
    options.name_legacy = True
    options.name_languages = ['*']
    options.layout_features = ['*']
    subsetter = subset.Subsetter(options=options)
    subsetter.populate(text=characters)
    subsetter.subset(font)
    font.save(OUTPUT_DIRECTORY / output_name)
    font.close()


def main() -> None:
    """解析可覆盖的字体路径，并在项目资源目录重建字体。"""
    windows_fonts = Path(os.environ.get('WINDIR', 'C:/Windows')) / 'Fonts'
    parser = ArgumentParser(description=__doc__)
    parser.add_argument('--newsreader', type=Path, default=REFERENCE_DIRECTORY / 'Fonts/NewsreaderVariable.ttf')
    parser.add_argument('--chinese', type=Path, default=windows_fonts / 'msyh.ttc')
    parser.add_argument('--chinese-bold', type=Path, default=windows_fonts / 'msyhbd.ttc')
    arguments = parser.parse_args()

    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    characters = (REFERENCE_DIRECTORY / 'HueRelay.html').read_text(encoding='utf-8')
    characters += ''.join(chr(code) for code in range(32, 127))
    rebuild_newsreader(arguments.newsreader)
    subset_chinese(arguments.chinese, 'ChineseGame.ttf', characters)
    subset_chinese(arguments.chinese_bold, 'ChineseGameBold.ttf', characters)
    print(f'字体已生成到 {OUTPUT_DIRECTORY}')


if __name__ == '__main__':
    main()

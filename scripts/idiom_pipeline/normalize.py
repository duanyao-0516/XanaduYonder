"""Conservative text and Mandarin syllable normalization (Python 3.9+)."""

import re
import unicodedata


_PLACEHOLDERS = {"", "无", "暂无", "无。", "暂无。", "不详"}
_TONES = frozenset("\u0300\u0301\u0304\u030c")
# Explicit syllables avoid accepting arbitrary initial/final combinations or typos.
# Umlaut is kept in display text; only the comparison keys spell it as v.
_SYLLABLES = frozenset("""
a ai an ang ao ba bai ban bang bao bei ben beng bi bian biao bie bin bing bo bu
ca cai can cang cao ce cen ceng cha chai chan chang chao che chen cheng chi chong
chou chu chua chuai chuan chuang chui chun chuo ci cong cou cu cuan cui cun cuo
da dai dan dang dao de dei den deng di dia dian diao die ding diu dong dou du
duan dui dun duo e ei en eng er fa fan fang fei fen feng fo fou fu ga gai gan
gang gao ge gei gen geng gong gou gu gua guai guan guang gui gun guo ha hai han
hang hao he hei hen heng hong hou hu hua huai huan huang hui hun huo ji jia jian
jiang jiao jie jin jing jiong jiu ju juan jue jun ka kai kan kang kao ke kei ken
keng kong kou ku kua kuai kuan kuang kui kun kuo la lai lan lang lao le lei leng
li lia lian liang liao lie lin ling liu lo long lou lu luan lun luo lv lve
ma mai man mang mao me mei men meng mi mian miao mie min ming miu mo mou mu
na nai nan nang nao ne nei nen neng ni nian niang niao nie nin ning niu nong nou
nu nuan nuo nv nve o ou pa pai pan pang pao pei pen peng pi pian piao pie pin
ping po pou pu qi qia qian qiang qiao qie qin qing qiong qiu qu quan que qun
ran rang rao re ren reng ri rong rou ru ruan rui run ruo sa sai san sang sao
se sen seng sha shai shan shang shao she shei shen sheng shi shou shu shua shuai
shuan shuang shui shun shuo si song sou su suan sui sun suo ta tai tan tang tao
te teng ti tian tiao tie ting tong tou tu tuan tui tun tuo wa wai wan wang wei
wen weng wo wu xi xia xian xiang xiao xie xin xing xiong xiu xu xuan xue xun
ya yan yang yao ye yi yin ying yo yong you yu yuan yue yun za zai zan zang zao
ze zei zen zeng zha zhai zhan zhang zhao zhe zhei zhen zheng zhi zhong zhou zhu
zhua zhuai zhuan zhuang zhui zhun zhuo zi zong zou zu zuan zui zun zuo
""".split())
_FOUR_HAN = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff\U00020000-\U0002fa1f]{4}\Z")


def clean_text(value):
    """Return NFC text or None for missing values; never stringify malformed data."""
    if value is None:
        return None
    if not isinstance(value, str):
        raise ValueError("Expected text or null")
    text = unicodedata.normalize("NFC", value).strip()
    return None if text in _PLACEHOLDERS else text


def equivalence_key(text):
    """Comparison only: retain letters/symbols and remove punctuation/whitespace."""
    return "".join(c for c in unicodedata.normalize("NFC", text or "")
                   if not c.isspace() and not unicodedata.category(c).startswith("P"))


def pinyin_display(value):
    """Normalize mechanical variants without guessing missing letters or tones."""
    text = clean_text(value)
    if text is None:
        raise ValueError("Missing pinyin")
    normalized = unicodedata.normalize("NFKC", text).lower().replace("ɡ", "g").replace(",", " ")
    return " ".join(normalized.split())


def parse_pinyin(value):
    display = pinyin_display(value)
    syllables = display.split()
    if len(syllables) != 4:
        raise ValueError("Pinyin must contain four syllables")
    keys = []
    for syllable in syllables:
        decomposed = unicodedata.normalize("NFD", syllable)
        if sum(c in _TONES for c in decomposed) > 1:
            raise ValueError("Multiple tones in one syllable")
        base = unicodedata.normalize("NFC", "".join(c for c in decomposed if c not in _TONES))
        key = base.replace("ü", "v")
        if key not in _SYLLABLES or "v" in base:
            raise ValueError("Unrecognized standard pinyin syllable: " + syllable)
        # a/e take precedence; ou marks o; otherwise the last vowel carries tone.
        # Inspect the original position rather than silently moving an invalid mark.
        if any(c in _TONES for c in decomposed):
            expected = (base.index("a") if "a" in base else base.index("e") if "e" in base
                        else base.index("o") if "ou" in base
                        else max(i for i, c in enumerate(base) if c in "aeiouü"))
            position = -1
            for char in decomposed:
                if not unicodedata.combining(char):
                    position += 1
                elif char in _TONES and position != expected:
                    raise ValueError("Tone mark is not on its standard vowel")
        keys.append(key)
    return {"display": display, "syllables": syllables, "keys": keys}


def is_four_han(word):
    return bool(_FOUR_HAN.fullmatch(word))

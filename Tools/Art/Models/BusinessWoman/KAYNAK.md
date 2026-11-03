# Kaynak ve kullanım hakkı

İş kadını modeli (`BusinessWoman.obj`) ayrı bir kaynaktan gelmedi: `Tools/Art/make_businesswoman.py`,
proje sahibinin yüklediği iş adamı modelinden (`Tools/Art/Models/BusinessMan`, bkz. oradaki `KAYNAK.md`)
türetir. Sakal, bıyık, favoriler ve kabarık perçem kaldırılır; uzun saç kabuğu, yan perçem, küpeler ve diz
boyu etek eklenir; vücut belden aşağı incelir, kot koyu çorap, spor ayakkabı koyu düz ayakkabı, tişört
referans resimdeki gibi mürdüm bluz olur. Ceket, eller, yüz ve tablet aynı kalır; ikisi aynı tarzda durur.

Hak durumu iş adamıyla aynıdır (türev iş); Steam içerik anketinde iş adamıyla birlikte "önceden üretilmiş
yapay zekâ içeriği" olarak beyan edilir (`Steam/README.md` §6).

Yeniden üretmek için depo kökünden:

    python3 Tools/Art/make_businesswoman.py
    python3 Tools/Art/make_character.py woman

İkinci komut oyundaki kopyayı yazar: `Assets/_Lazpoly/Data/Resources/Lazpoly/Models/business_woman.txt`.

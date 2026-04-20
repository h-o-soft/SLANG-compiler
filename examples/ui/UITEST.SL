///
/// UITEST.SL — UILIB 静的 HUD デモ
///
/// 文字列 / FILL / BOX の描画確認。
/// 日本語は examples/ui/strings.sl (tools/charmap-encode.py で事前生成)。
///

#include UILIB.LIB
#include "strings.sl"

MAIN()
{
	WIDTH(40);
	GRPSETUP();
	GRDISP(1);
	GRCLS();

	// UILIB 単独テストなので SPR_INIT は呼ばないが、単独使用時は
	// 事前に GVRAM をクリアしておくと背景が見やすい。
	// (SPRLIB を併用するアプリではここで SPR_INIT() を呼ぶこと)

	// タイトル (白)
	UI_SET_COLOR(7);
	UI_AT(15, 1);
	UI_PUTS(MSG_TITLE);

	// 日本語 (緑)
	UI_SET_COLOR(4);
	UI_AT(15, 4);
	UI_PUTS(MSG_HELLO);

	// スコア枠 (シアン = B+G)
	UI_SET_COLOR(5);
	UI_AT(3, 10);
	UI_BOX(15, 4);

	// 枠内: ラベル + 数値 (白)
	UI_SET_COLOR(7);
	UI_AT(5, 11);
	UI_PUTS(MSG_SCORE);
	UI_AT(10, 11);
	UI_PUTC('1');
	UI_PUTC('2');
	UI_PUTC('3');
	UI_PUTC('4');
	UI_PUTC('5');

	UI_AT(5, 12);
	UI_PUTS(MSG_POWER);
	UI_AT(10, 12);
	UI_PUTC('7');
	UI_PUTC('7');

	// FILL 確認 (赤の横長バー)
	UI_SET_COLOR(2);
	UI_AT(0, 16);
	UI_FILL(40, 1);

	// 下部メッセージ (黄 = R+G)
	UI_SET_COLOR(6);
	UI_AT(13, 22);
	UI_PUTS(MSG_PRESS);

	LOOP {}
}

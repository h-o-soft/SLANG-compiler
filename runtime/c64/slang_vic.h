#ifndef SLANG_VIC_H
#define SLANG_VIC_H

// SLFS bridge: oscar64 c64/vic.h の薄い wrapper。
// vic_setmode は (VicMode mode, char* screen, char* font) → SLANG では
// (byte, word, word) で渡す。

void slang_vic_setmode(unsigned char mode, unsigned int screen, unsigned int font);
void slang_vic_setbank(unsigned char bank);

#endif

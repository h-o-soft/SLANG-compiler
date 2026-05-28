#include <c64/vic.h>
#include "slang_vic.h"

// VIC-II text/bitmap mode 切替。 screen / font は VIC bank 0 から見える
// memory address (= 例 VICM_TEXT で screen=$0400, font=$1000)。
// I/O register 叩くため I/O visible な mmap (= MMAP_NO_BASIC 等) 必須。
void slang_vic_setmode(unsigned char mode, unsigned int screen, unsigned int font) {
    vic_setmode((VicMode)mode, (const char *)screen, (const char *)font);
}

// VIC bank 切替 (= 16KB 単位、 0=$0000-$3FFF, 1=$4000-$7FFF, 2=$8000-$BFFF, 3=$C000-$FFFF)。
void slang_vic_setbank(unsigned char bank) {
    vic_setbank(bank);
}

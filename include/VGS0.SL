
const VGS0_JOYPAD_UP = 10000000b;
const VGS0_JOYPAD_DW = 01000000b;
const VGS0_JOYPAD_LE = 00100000b;
const VGS0_JOYPAD_RI = 00010000b;
const VGS0_JOYPAD_ST = 00001000b;
const VGS0_JOYPAD_SE = 00000100b;
const VGS0_JOYPAD_T1 = 00000010b;
const VGS0_JOYPAD_T2 = 00000001b;

// TODO これがWORK側に行くよう変更

// ; Variables (has initial values)
// ↑これをWORKの下にするだけで良さそう
ARRAY BYTE VGS0_ADDR_BG[]:$8000;
ARRAY BYTE VGS0_ADDR_BG_ATTR[]:$8400;
ARRAY BYTE VGS0_ADDR_FG[]:$8800;
ARRAY BYTE VGS0_ADDR_FG_ATTR[]:$8C00;
ARRAY BYTE VGS0_ADDR_OAM[]:$9000;

ARRAY WORD VGS0_ADDR_PALETTE[]:$9800;

VAR BYTE VGS0_ADDR_COUNT_V:$9F00;
VAR BYTE VGS0_ADDR_COUNT_H:$9F01;

VAR BYTE VGS0_ADDR_BG_SCROLL_X:$9F02;
VAR BYTE VGS0_ADDR_BG_SCROLL_Y:$9F03;
VAR BYTE VGS0_ADDR_FG_SCROLL_X:$9F04;
VAR BYTE VGS0_ADDR_FG_SCROLL_Y:$9F05;

VAR BYTE VGS0_ADDR_IRQ_SCANLINE:$9F06;
VAR BYTE VGS0_ADDR_STATUS:$9F07;

VAR BYTE VGS0_ADDR_CHARACTER:$A000;

VAR BYTE VGS0_ADDR_BG_DPM:$9F08;
VAR BYTE VGS0_ADDR_FG_DPM:$9F09;
VAR BYTE VGS0_ADDR_SPRITE_DPM:$9F0A;

VAR BYTE VGS0_ADDR_PTN1024:$9F0B;

CONST OAM_Y = 0;
CONST OAM_X = 1;
CONST OAM_PTN = 2;
CONST OAM_ATTR = 3;
CONST OAM_HEIGHT = 4;
CONST OAM_WIDTH = 5;
CONST OAM_BANK = 6;
CONST OAM_RESERVED = 7;

// SLANGでは実現不可能な部分のCライブラリの諸々
////! Real number type with fixed-point numbers
//typedef union {
//    uint16_t value;
//    uint8_t raw[2];
//} var16_t;
//
////! Rentangle (signed position)
//typedef struct {
//    int8_t x;
//    int8_t y;
//    uint8_t width;
//    uint8_t height;
//} rect_t;
//
////! Rentangle (unsigned position)
//typedef struct {
//    uint8_t x;
//    uint8_t y;
//    uint8_t width;
//    uint8_t height;
//} urect_t;
//
///**
// * Object Attribute Memory
// * https://github.com/suzukiplan/vgszero/blob/master/README.md#oam
// */
//typedef struct {
//    //! Y-coordinate
//    uint8_t y;
//    //! X-coordinate
//    uint8_t x;
//    //! Character pattern number: https://github.com/suzukiplan/vgszero/blob/master/README.md#character-pattern-table
//    uint8_t ptn;
//    //! Attribute value: https://github.com/suzukiplan/vgszero/blob/master/README.md#attribute
//    uint8_t attr;
//    //! OAM Pattern Size (height - 1)
//    uint8_t heightMinus1;
//    //! OAM Pattern Size (width - 1)
//    uint8_t widthMinus1;
//    //! OAM Bank
//    uint8_t bank;
//    //! padding
//    uint8_t reserved;
//} OAM;
//
///**
// * Name Table
// */
//typedef struct {
//    //! Character pattern number: https://github.com/suzukiplan/vgszero/blob/master/README.md#character-pattern-table
//    uint8_t ptn[32][32];
//    //! Attribute value: https://github.com/suzukiplan/vgszero/blob/master/README.md#attribute
//    uint8_t attr[32][32];
//} NameTable;

vgs0_palette_set(pn, pi, r, g, b)
VAR col;
VAR addr;
{
    col = r and 0x1F;
    col = col << 5;
    col = col or (g and 0x1F);
    col = col << 5;
    col = col or (b and 0x1F);
    addr = 0x9800;
    addr += (pn << 5);
    addr += (pi << 1);
    MEMW[addr] = col;
}

vgs0_palette_set_rgb555(pn, pi, rgb555)
VAR addr;
{
    addr = 0x9800;
    addr += pn << 5;
    addr += pi << 1;
    MEMW[addr] = rgb555;
}

vgs0_angle(sx, sy, dx, dy)
VAR x,y;
{
    y = sy - dy;
    x = sx - dx;
    return vgs0_atan2b(y, x) - 64;
}

vgs0_bg_putstr(x, y, attr, ptr)
VAR addrC;
VAR addrA;
{
    x = x and $1f;
    y = y and $1f;
    addrC = $8000 + (y << 5) + x;
    addrA = addrC + 0x400;
    while MEM[ptr] != 0 {
        MEM[addrC] = MEM[ptr];
        MEM[addrA] = attr;
        addrC++;
        addrA++;
        ptr = ptr + 1;
    }
}

vgs0_fg_putstr(x, y, attr, ptr)
VAR addrC;
VAR addrA;
{
    x = x and $1f;
    y = y and $1f;
    addrC = 0x8800 + (y << 5) + x;
    addrA = addrC + 0x400;
    while MEM[ptr] != 0 {
        MEM[addrC] = MEM[ptr];
        MEM[addrA] = attr;
        addrC++;
        addrA++;
        ptr = ptr + 1;
    }
}

// typedef struct {
//     uint8_t ptn[32][32];
//     uint8_t attr[32][32];
// } NameTable;
vgs0_putstr(namtbl, x, y, attr, str)
VAR BYTE namtbl_ptn[][32-1];
VAR BYTE namtbl_attr[][32-1];
{
    x = x and $1F;
    y = y and $1F;
    namtbl_ptn = namtbl;
    namtbl_attr = namtbl + 32*32;
    while MEM[str] != 0 {
        namtbl_ptn[y][x] = MEM[str];
        namtbl_attr[y][x] = attr;
        x++;
        x = x and $1F;
        str++;
    }
}

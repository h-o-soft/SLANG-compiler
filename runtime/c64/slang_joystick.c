/*
 * SLANG joystick bridge for C64 (oscar64).
 * See slang_joystick.h for API contract.
 */

#include "slang_joystick.h"
#include <c64/joystick.h>

void slang_joy_poll(unsigned char port)
{
    joy_poll(port);
}

unsigned int slang_joy_x(unsigned char port)
{
    /* oscar64 joyx は sbyte (-1/0/1)。SLANG WORD は unsigned なので
     * signed → int → unsigned の二段 cast で -1 を $FFFF として渡す。 */
    return (unsigned int)(int)joyx[port];
}

unsigned int slang_joy_y(unsigned char port)
{
    return (unsigned int)(int)joyy[port];
}

unsigned int slang_joy_b(unsigned char port)
{
    /* oscar64 joyb は bool (= char)。0/1 を unsigned int で返す。 */
    return (unsigned int)(joyb[port] ? 1 : 0);
}

unsigned int slang_joy_dir(unsigned char port)
{
    /* C64_JOY.LIB の CONST と整合する 5 bit bitmask:
     *   bit 0 UP / 1 DOWN / 2 LEFT / 3 RIGHT / 4 FIRE
     * joyx[port] / joyy[port] (-1/0/1) と joyb[port] (0/1) から組み立てる。 */
    unsigned int mask = 0;
    int x = (int)joyx[port];
    int y = (int)joyy[port];
    if (y < 0) mask |= 0x01;   /* UP    */
    if (y > 0) mask |= 0x02;   /* DOWN  */
    if (x < 0) mask |= 0x04;   /* LEFT  */
    if (x > 0) mask |= 0x08;   /* RIGHT */
    if (joyb[port]) mask |= 0x10;   /* FIRE  */
    return mask;
}

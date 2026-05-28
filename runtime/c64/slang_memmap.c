#include <c64/memmap.h>
#include "slang_memmap.h"

// mmap_set: 旧 mmap 値 (= 復元用) を返却、 oscar64 spec 通り。
unsigned char slang_mmap_set(unsigned char mode) {
    return (unsigned char)mmap_set(mode);
}

// mmap_trampoline: IRQ/NMI が KERNAL ROM 不在 mmap でも動くよう trampoline install。
void slang_mmap_trampoline(void) {
    mmap_trampoline();
}

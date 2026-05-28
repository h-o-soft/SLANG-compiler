#ifndef SLANG_MEMMAP_H
#define SLANG_MEMMAP_H

// SLANG bridge: oscar64 c64/memmap.h の薄い wrapper。
// SLANG ABI に合わせて unsigned char で受け、 oscar64 API へ pass-through。

unsigned char slang_mmap_set(unsigned char mode);
void slang_mmap_trampoline(void);

#endif

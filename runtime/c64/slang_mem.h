#ifndef SLANG_MEM_H
#define SLANG_MEM_H

// SLANG bridge: oscar64 string.h memcpy/memset の薄い wrapper。
// 現在の CPU memory map に従う (= $D000-$DFFF は mmap 状態次第)、
// I/O register 系を触る場合は適切な mmap で呼ぶ必要。

void slang_memcpy(unsigned int dst, unsigned int src, unsigned int size);
void slang_memset(unsigned int dst, unsigned char val, unsigned int size);

#endif

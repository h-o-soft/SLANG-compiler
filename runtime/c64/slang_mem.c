#include <string.h>
#include "slang_mem.h"

void slang_memcpy(unsigned int dst, unsigned int src, unsigned int size) {
    memcpy((void *)dst, (const void *)src, size);
}

void slang_memset(unsigned int dst, unsigned char val, unsigned int size) {
    memset((void *)dst, val, size);
}

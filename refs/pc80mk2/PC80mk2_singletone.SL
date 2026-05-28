ORG $C000

MACHINE TONE_TEST(0):ToneTest;

MAIN()
{
  TONE_TEST();
  LOOP {
	IF NOT(KEYCHK(9) AND 0x80) THEN EXIT;
  }
  SND_KILL();
}

#ASM
ToneTest:
    ld   a, %00110110     ; SNDWRT.@1 (= CH1, mode 3, latch L/H)
    out  (0x0F), a
    ld   a, 0x88          ; freq low  (= CH1 のラ/A4 相当の counter 値)
    out  (0x0C), a
    ld   a, 0x0D
    out  (0x0C), a        ; freq high
    ld   a, %00001000     ; KEYON CH1 only
    out  (0x02), a
    ret

SND_KILL:
    xor  a
    out  (0x02), a        ; KEYON 全 OFF
    ret
#END

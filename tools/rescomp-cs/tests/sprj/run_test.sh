#!/bin/sh
# [EXT] SPRX: SPRJ converter + sprite_eng_ext.c test (Linux).
# Requires: dotnet 8 SDK, m68k-linux-gnu-gcc, qemu-m68k (Ubuntu: dotnet-sdk-8.0 gcc-m68k-linux-gnu qemu-user)
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
SGDK=$(cd "$HERE/../../../.." && pwd)
OUT=${OUT:-/tmp/sprj_test}
mkdir -p "$OUT"

dotnet build "$SGDK/tools/rescomp-cs/src/RescompCs.csproj" -c Release -o "$OUT/rescomp" >/dev/null
(cd "$HERE" && dotnet "$OUT/rescomp/rescomp.dll" test.res "$OUT/test.s" > "$OUT/rescomp.log")

# same assembler options as SGDK / surugacc ('|' is bitwise or, not a comment)
cp "$OUT/test.s" "$OUT/test.rs"
m68k-linux-gnu-gcc -m68000 -x assembler-with-cpp -Wa,--register-prefix-optional,--bitwise-or,--noexecstack -c "$OUT/test.rs" -o "$OUT/test_s.o"
CFLAGS="-DSGDK_GCC -O1 -Wall -Wno-unused-parameter -fno-builtin -fms-extensions -I$SGDK/inc -I$SGDK/res -I$OUT"
m68k-linux-gnu-gcc $CFLAGS -c "$SGDK/src/ext/sprite_eng_ext.c" -o "$OUT/sprite_eng_ext.o"
m68k-linux-gnu-gcc $CFLAGS -c "$HERE/sprx_test.c" -o "$OUT/sprx_test.o"
m68k-linux-gnu-gcc -static -o "$OUT/sprx_test" "$OUT/sprx_test.o" "$OUT/sprite_eng_ext.o" "$OUT/test_s.o"
qemu-m68k "$OUT/sprx_test"

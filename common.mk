# Common definitions

ifeq ($(GDK),)
	BIN := bin
	LIB := lib

	SRC_LIB := src
	RES_LIB := res
	INC_LIB := inc
else ifeq ($(GDK),.)
	BIN := bin
	LIB := lib

	SRC_LIB := src
	RES_LIB := res
	INC_LIB := inc
else
	BIN := $(GDK)/bin
	LIB := $(GDK)/lib

	SRC_LIB := $(GDK)/src
	RES_LIB := $(GDK)/res
	INC_LIB := $(GDK)/inc
endif

ifeq ($(OS),Windows_NT)
	# Native Windows
	SHELL := $(BIN)/sh
	RM := $(BIN)/rm
	CP := $(BIN)/cp
	MKDIR := $(BIN)/mkdir

	AR := $(BIN)/ar
	CC := $(BIN)/gcc
	LD:= $(BIN)/ld
	NM:= $(BIN)/nm
	OBJCPY := $(BIN)/objcopy
	CONVSYM := $(BIN)/convsym
	ASMZ80 := $(BIN)/sjasm
	MACCER := $(BIN)/mac68k
	BINTOS := $(BIN)/bintos
	LTO_PLUGIN := --plugin=liblto_plugin.dll
	LIBGCC := $(LIB)/libgcc.a
else
	# Native Linux and Docker
	PREFIX ?= m68k-elf-
	SHELL := sh
	RM := rm
	CP := cp
	MKDIR := mkdir

	AR := $(PREFIX)ar
	CC := $(PREFIX)gcc
	LD := $(PREFIX)ld
	NM := $(PREFIX)nm
	OBJCPY := $(PREFIX)objcopy
	CONVSYM := convsym
	ASMZ80 := sjasm
	MACCER := mac68k
	BINTOS := bintos
	LTO_PLUGIN :=
	LIBGCC := -lgcc
endif

JAVA := java
ECHO := echo
SIZEBND := $(JAVA) -jar $(BIN)/sizebnd.jar
RESCOMP := $(JAVA) -jar $(BIN)/rescomp.jar

# ---------------------------------------------------------------------------
# Debug build settings (used by the 'debug' target of makefile.gen and makelib.gen)
# Override them on the command line, e.g.
#   make -f makelib.gen debug DWARF_VERSION=5 DEBUG_OPT=-Og
#   make -f $(GDK)/makefile.gen debug DWARF_VERSION=5 DEBUG_OPT=-Og
# Build the library and the project with the same values (the project build warns otherwise).
#
#   DWARF_VERSION      4 (default) or 5: -gdwarf-N for C and for assembly (.s, sega.s, resources)
#   DEBUG_OPT          -O1 (default, as before), -Og or -O0 (-O0 keeps every variable and line)
#   DEBUG_PREFIX_SGDK  the SGDK folder is written as this in the debug info (default: sgdk).
#                      The project folder is written as '.' (paths relative to the project).
#   DIAG_FORMAT        when set, compiler messages use -fdiagnostics-format=$(DIAG_FORMAT)
#                      (json, json-file, sarif-stderr, sarif-file) in every build type
# ---------------------------------------------------------------------------
DWARF_VERSION ?= 4
DEBUG_OPT ?= -O1
DEBUG_PREFIX_SGDK ?= sgdk
DIAG_FORMAT ?=

ifeq ($(filter 4 5,$(DWARF_VERSION)),)
$(error DWARF_VERSION must be 4 or 5 (got '$(DWARF_VERSION)'))
endif
ifeq ($(filter -O0 -Og -O1 -O2 -Os,$(DEBUG_OPT)),)
$(error DEBUG_OPT must be -O0, -Og or -O1 (got '$(DEBUG_OPT)'))
endif

# -Wa,--gdwarf-N: the assembler writes the line table (.debug_line) in the same DWARF version
# and also writes line info for assembly sources (it has no effect on .debug_info of C files)
DEBUG_INFO_FLAGS := -ggdb -gdwarf-$(DWARF_VERSION) -Wa,--gdwarf-$(DWARF_VERSION)
# name of the marker file that records the settings a debug build was made with
DEBUG_CFG := dwarf$(DWARF_VERSION)$(DEBUG_OPT)

# absolute path of a folder as gcc sees it (with the MSYS make/sh of SGDK on Windows: C:/...)
native_path = $(shell cd "$(1)" && (pwd -W 2>/dev/null || pwd))
# map folder $(1) to $(2) in the debug info, for the compiler and for the assembler
debug_prefix_map = -fdebug-prefix-map=$(1)=$(2) -Wa,--debug-prefix-map,$(1)=$(2)

ifeq ($(DIAG_FORMAT),)
	DIAG_FLAGS :=
else
	DIAG_FLAGS := -fdiagnostics-format=$(DIAG_FORMAT)
endif

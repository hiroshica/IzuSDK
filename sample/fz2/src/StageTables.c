
#include <common.h>

// clang-format off

#include "Stage1Data.dtc"

Stage_Record *StageDataTables[STAGE_MAX] =
{
    &Stage1,
    &Stage1,
    &Stage1,
    &Stage1,
    &Stage1,
    &Stage1,
    &Stage1,
    &Stage1,
};

StageStartData StageStartDataTable[STAGE_MAX] =
{
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
    { 0, 3 },
};

//-----------------------------------------------------------------------------------------------------------------
// clang-format on

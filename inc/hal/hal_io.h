/**
 *  \file hal/hal_io.h
 *  \brief I/O ports hardware layer (HAL): controller ports
 *
 * [V1] L0 HAL (sgd::hal::io). Port 0 = PORT_1, 1 = PORT_2, 2 = EXT port. All macros.
 * The protocol (3/6 buttons, mouse, team player...) is middleware (src/mw/joy.c).
 */

#ifndef _HAL_IO_H_
#define _HAL_IO_H_

#include "types.h"

#define HAL_IO_VERSION_PORT                 0xA10001
#define HAL_IO_DATA_PORT(port)              (0xA10003 + ((port) * 2))
#define HAL_IO_CTRL_PORT(port)              (0xA10009 + ((port) * 2))

/** \brief Pointer to the data port of a controller port */
#define hal_io_data_ptr(port)               ((vu8*) HAL_IO_DATA_PORT(port))
/** \brief Pointer to the control port of a controller port */
#define hal_io_ctrl_ptr(port)               ((vu8*) HAL_IO_CTRL_PORT(port))
/** \brief Read the data port */
#define hal_io_read_data(port)              (*hal_io_data_ptr(port))
/** \brief Write the data port (TH / TR / TL lines when set as output) */
#define hal_io_write_data(port, v)          (*hal_io_data_ptr(port) = (u8) (v))
/** \brief Write the control port (bit set = output line, bit 7 = TH interrupt enable) */
#define hal_io_write_ctrl(port, v)          (*hal_io_ctrl_ptr(port) = (u8) (v))
/** \brief Read the control port */
#define hal_io_read_ctrl(port)              (*hal_io_ctrl_ptr(port))
/** \brief Hardware version register */
#define hal_io_version()                    (*((vu8*) HAL_IO_VERSION_PORT))

#endif // _HAL_IO_H_

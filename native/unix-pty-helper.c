#include <errno.h>
#include <sys/types.h>
#include <unistd.h>

#if defined(__APPLE__)
#include <sys/ioctl.h>
#include <util.h>
#else
#include <pty.h>
#include <sys/ioctl.h>
#endif

#if defined(__GNUC__) || defined(__clang__)
#define LIBGHOSTTY_EXPORT __attribute__((visibility("default")))
#else
#define LIBGHOSTTY_EXPORT
#endif

LIBGHOSTTY_EXPORT int muxer_forkpty_exec(
    int *master_file_descriptor,
    const char *working_directory,
    const char *executable,
    char *const arguments[],
    char *const environment[],
    unsigned short rows,
    unsigned short columns
)
{
    struct winsize window_size = {
        .ws_row = rows,
        .ws_col = columns,
        .ws_xpixel = 0,
        .ws_ypixel = 0,
    };
    const pid_t process_id = forkpty(
        master_file_descriptor,
        NULL,
        NULL,
        &window_size
    );
    if (process_id < 0) {
        return -errno;
    }

    /* The child must finish in native code after fork; re-entering a multithreaded CLR is unsafe. */
    if (process_id == 0) {
        if (chdir(working_directory) != 0) {
            _exit(126);
        }

        execve(executable, arguments, environment);
        _exit(127);
    }

    return process_id;
}

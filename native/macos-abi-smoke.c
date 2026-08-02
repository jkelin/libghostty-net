#include <ghostty/vt.h>
#include <stddef.h>

typedef GhosttyResult (*ghostty_terminal_new_fn)(
    const GhosttyAllocator *,
    GhosttyTerminal *,
    GhosttyTerminalOptions
);
typedef int (*muxer_forkpty_exec_fn)(
    int *master_file_descriptor,
    const char *working_directory,
    const char *executable,
    char *const arguments[],
    char *const environment[],
    unsigned short rows,
    unsigned short columns
);

extern int muxer_forkpty_exec(
    int *master_file_descriptor,
    const char *working_directory,
    const char *executable,
    char *const arguments[],
    char *const environment[],
    unsigned short rows,
    unsigned short columns
);

static ghostty_terminal_new_fn volatile terminal_new_reference = ghostty_terminal_new;
static muxer_forkpty_exec_fn volatile forkpty_reference = muxer_forkpty_exec;

int main(void)
{
    return terminal_new_reference == NULL || forkpty_reference == NULL;
}

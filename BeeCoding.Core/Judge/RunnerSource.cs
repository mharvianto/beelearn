namespace BeeCoding.Services.Judge;

/// <summary>
/// Source of the tiny privileged-free "runner" helper. It forks, applies rlimits
/// (CPU, address space, stack, file size, process count), execs the target, and
/// wait4()s it to capture peak RSS + wall time. An alarm() backstops wall time.
///
/// Output line written to STATFILE:  "&lt;exitcode&gt; &lt;termsig&gt; &lt;maxrss_kb&gt; &lt;wall_ms&gt;"
/// (exitcode is -1 when the process was killed by a signal).
/// </summary>
public static class RunnerSource
{
    public const string C = """
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <signal.h>
#include <time.h>
#include <errno.h>
#include <sys/resource.h>
#include <sys/wait.h>
#include <sys/types.h>

/* runner STATFILE cpuSec wallSec asKB stackKB fsizeKB nproc PROG [args...] */

static pid_t child = -1;
static void on_alarm(int s){ (void)s; if(child>0){ kill(-child, SIGKILL); } }

int main(int argc, char** argv){
    if(argc < 9){ fprintf(stderr,"runner: bad args\n"); return 2; }
    const char* statfile = argv[1];
    long cpuSec  = atol(argv[2]);
    long wallSec = atol(argv[3]);
    long asKB    = atol(argv[4]);
    long stackKB = atol(argv[5]);
    long fsizeKB = atol(argv[6]);
    long nproc   = atol(argv[7]);
    char** cmd   = &argv[8];

    struct timespec t0,t1;
    clock_gettime(CLOCK_MONOTONIC,&t0);

    child = fork();
    if(child < 0){ perror("fork"); return 3; }
    if(child == 0){
        setpgid(0,0);
        struct rlimit rl;
        if(cpuSec>0){ rl.rlim_cur=cpuSec; rl.rlim_max=cpuSec+1; setrlimit(RLIMIT_CPU,&rl); }
        if(asKB>0){ rl.rlim_cur=rl.rlim_max=(rlim_t)asKB*1024; setrlimit(RLIMIT_AS,&rl); }
        if(stackKB>0){ rl.rlim_cur=rl.rlim_max=(rlim_t)stackKB*1024; setrlimit(RLIMIT_STACK,&rl); }
        if(fsizeKB>0){ rl.rlim_cur=rl.rlim_max=(rlim_t)fsizeKB*1024; setrlimit(RLIMIT_FSIZE,&rl); }
        if(nproc>0){ rl.rlim_cur=rl.rlim_max=nproc; setrlimit(RLIMIT_NPROC,&rl); }
        rl.rlim_cur=rl.rlim_max=0; setrlimit(RLIMIT_CORE,&rl);
        execvp(cmd[0], cmd);
        _exit(127);
    }
    setpgid(child, child);
    signal(SIGALRM, on_alarm);
    if(wallSec>0) alarm((unsigned)wallSec);

    int status; struct rusage ru;
    while(wait4(child,&status,0,&ru) < 0){ if(errno!=EINTR){ perror("wait4"); return 4; } }
    clock_gettime(CLOCK_MONOTONIC,&t1);
    long wallMs = (t1.tv_sec-t0.tv_sec)*1000 + (t1.tv_nsec-t0.tv_nsec)/1000000;

    int exitcode = -1, termsig = 0;
    if(WIFEXITED(status))   exitcode = WEXITSTATUS(status);
    if(WIFSIGNALED(status)) termsig  = WTERMSIG(status);

    FILE* f = fopen(statfile,"w");
    if(!f){ perror("fopen stat"); return 5; }
    fprintf(f,"%d %d %ld %ld\n", exitcode, termsig, ru.ru_maxrss, wallMs);
    fclose(f);
    return 0;
}
""";
}

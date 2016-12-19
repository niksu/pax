/*
Timer state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;
using PacketDotNet;

namespace Pax_TCP {
  public enum Timer_State { Free, Started, Stopped }

  /*
    set: set the interval value. default = 0.
    start: start the timer with the set interval value
    stop: stop the timer
    reset: restart timer with the most recently set interval value -- dropped this since didn't seem necessary
    free: stop and mark free
    when timer expires, it:
      1. runs code
      and optionally (depending on parameter)
      2. restarts
  */
  public class TimerCB {
    public static ConcurrentQueue<Tuple<Packet,TimerCB>> timer_q;

    Timer_State state;
    uint interval;
    Timer timer;

    // FIXME add nullary constructor that initialises TCB.
    public TimerCB() {
      Debug.Assert(TimerCB.timer_q != null);
      this.state = Timer_State.Free;
    }

    public void set_interval (uint interval) {
      this.interval = interval;
    }

    public void set_action (/*FIXME how to encode action information*/) {
      // FIXME how to store action info.
      throw new Exception("TODO");
    }

    public void stop() {
      // timer.
      this.state = Timer_State.Stopped;
      throw new Exception("TODO");
    }

    public void start() {
      // timer = new Timer(Flush, null, 0, interval);
      this.state = Timer_State.Started;
      throw new Exception("TODO");
    }

    public void free() {
      stop();
      this.state = Timer_State.Free;
    }

    private void act(Object o) {
      // FIXME carry out action.
      throw new Exception("TODO");
    }


    // Negative values indicate that the lookup failed.
    public static int lookup (TimerCB[] timer_cbs, Packet packet) {
      // FIXME
      throw new Exception("TODO");
    }

    public static int find_free_TimerCB(TimerCB[] timer_cbs) {
      // FIXME linear search not efficient.
      for (int i = 0; i < timer_cbs.Length; i++) { // NOTE assuming that timer_cbs.Length == max_timers
        if (timer_cbs[i].state == Timer_State.Free) {
          return i;
        }
      }

      return -1;
    }
  }
}

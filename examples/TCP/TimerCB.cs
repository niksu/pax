/*
Timer state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Timers;
using PacketDotNet;

namespace Pax_TCP {
  public enum Timer_State { Free, Started, Stopped }

  /*
    set_interval: set the interval value.
    set_action: set the action to carry out when timer expires.
    start: start the timer with the set interval value. asserts that interval > 0 and that an action has been set.
    stop: stop the timer -- i don't think we need this to be public, so at the moment it's private.
    free: stop and mark free.
    when timer expires, it carries out some action, and runs "free".

    as for cross-referencing between different types of data, i think:
    * need reference from packets to related timers (for retransmission)
    * and from TCB to packets (relating to that connection)
  */
  public class TimerCB {
    public static ConcurrentQueue<Tuple<Packet,TimerCB>> timer_q;

    Timer_State state;
    Timer timer = new Timer();

    // FIXME add nullary constructor that initialises TCB.
    public TimerCB() {
      Debug.Assert(TimerCB.timer_q != null);
      this.state = Timer_State.Free;
      timer.Elapsed += act;
      timer.Enabled = true;
      timer.AutoReset = false;
    }

    public void set_interval (int interval) {
      Debug.Assert(interval > 0);
      timer.Interval = interval;
    }

    public void set_action (/*FIXME how to encode action information*/) {
      // FIXME how to store action info.
      throw new Exception("TODO");
    }

    public void start() {
      timer.Start();
      this.state = Timer_State.Started;
    }

    private void stop() {
      timer.Stop();
      this.state = Timer_State.Stopped;
    }

    public void free() {
      this.stop();
      this.state = Timer_State.Free;
    }

    private void act(Object source, ElapsedEventArgs e) {
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
        // FIXME protect against race conditions
        if (timer_cbs[i].state == Timer_State.Free) {
          return i;
        }
      }

      return -1;
    }
  }
}

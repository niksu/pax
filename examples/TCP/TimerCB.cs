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
  public enum Action { Retransmit, FreeTCB }

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

    private Object obj;
    private Action action;

    Timer_State state;
    Timer timer = new Timer();

    public TimerCB() {
      Debug.Assert(TimerCB.timer_q != null);
      this.state = Timer_State.Free;
      timer.Elapsed += act;
      timer.AutoReset = false;
    }

    public void set_interval (int interval) {
      Debug.Assert(interval > 0);
      timer.Interval = interval;
    }

    public void set_action (Action action, Object obj) {
      Debug.Assert((obj is TCB || obj is Packet) && obj != null);
      this.action = action;
      this.obj = obj;
    }

    public Action get_action () {
      return this.action;
    }

    public Action get_object () {
      return this.obj;
    }

    public void start() {
      Debug.Assert(!timer.Enabled); // timer shouldn't already be running.
      Debug.Assert(this.state != Timer_State.Free);
      timer.Start();
      this.state = Timer_State.Started;
    }

    private void stop() {
      Debug.Assert(timer.Enabled); // timer should already be running.
      Debug.Assert(this.state != Timer_State.Free);
      timer.Stop();
      this.state = Timer_State.Stopped;
    }

    public void free() {
      this.stop();
      this.state = Timer_State.Free;
      // FIXME how to ensure that TCB etc don't have stale reference to this
      //       timer resource, now that it has been freed?
    }

    private void act(Object source, ElapsedEventArgs e) {
      timer_q.Enqueue(new Tuple <Packet, TCB>(p.Item1, null/*FIXME lookup TCB*/));

      this.free();
      // Action involve enqueuing a command on timer_q, that will be
      // executed by a separate thread.
      switch (this.action) {
        case Action.Retransmit:
          break;

        case Action.FreeTCB:
          break;

        default:
          throw new Exception("Impossible");
      }
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

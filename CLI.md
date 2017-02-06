# Command-line interface
* `-v`: produce more verbose output.
* `-no-default`: by default Pax uses the "Dropper" element when the handler named in a .json file cannot be found, as well as emits a message. Using this flag results in a default not being substituted (but a message is still emitted), as a consequence of which Pax might crash complaining that the handler couldn't be found. For more see [examples/nonsense_wiring.json](examples/nonsense_wiring.json).

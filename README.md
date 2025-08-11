# SimpleKeyPresser

Simple windows application to simulate key presses until stopped. Can simulate multiple key presses at once.

![Example](/Assets/example.png)

## How to Run

1. (Optional) Compile exe

    ```sh
    dotnet build
    ```

2. Run the executable: `KeyPresser.exe` (or if you compiled yourself, `bin/Debug/../KeyPresser.exe`)

## How to Use

1. Enter the keys you want simulated, which can be alphanumeric or space.
2. Choose the hold and interval you'd like simulated:
    - Interval: how long to wait between key presses. A random value will be chosen each key press between the specified min/max.
    - Hold: how long the key is held. A random value will be chosen each key press between the specified min/max.
    - Choosing wider random intervals will decrease the script's predictability, making it harder to differentiate betweeen automated v. human inputs
3. Click Start to being the simulation. You should change your focus to the window you want the inputs simulated in
4. Click Stop when you're done
# BuzzTimer

A multiplayer timing and bluff game for PS3 wireless Buzz! controllers, built with Unity for Windows.

## How to play

1. Choose between two and four players.
2. Enter each player's name.
3. Assign a controller by asking each player to press their large red **BUZZ** button.
4. At the beginning of each round, the game displays a random target between `3.000` and `10.000` seconds.
5. The current player presses **green** to start or continue the hidden timer.
6. They press **red** whenever they want to stop the timer and pass the turn.
7. Before continuing, the next player can press **yellow** to challenge the previous player.

If the accumulated time has exceeded the target, the previous player is eliminated. If it has not exceeded the target, including an exact tie, the challenger is eliminated.

The next round starts with the player immediately after the eliminated player. A match has `number of players - 1` rounds, leaving one winner.

## Features

- Two to four local players
- Player names and controller assignment
- Hidden timer with millisecond precision
- Portuguese and English interface
- LED indication for the active controller
- Automatic reconnection to the Wbuzz receiver
- Rematch option that preserves names and controller assignments
- Safe HID shutdown when leaving the game

## Controls

| Button | Action |
| --- | --- |
| Green | Start or continue the hidden timer |
| Red | Stop the timer and pass the turn |
| Yellow | End the round and challenge the previous player |

## Requirements

- Windows 10 or Windows 11
- Sony wireless Buzz! receiver for PS3 (`VID 054C / PID 1000`)
- Two to four wireless Buzz! controllers

## Build from source

Open the project in Unity `6000.3.22f1` and select **Buzz Timer > Build Windows**. The Windows build will be created locally in `Builds/Windows`.

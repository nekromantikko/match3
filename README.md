# Match 3

A small match 3 game using Unity and UniRx where you match donuts, for UniRx practice/experiment. My design philosophy with this was to keep the game logic state separate from the rendering state, allowing the game to advance in discrete steps while the animations are smooth. Graphics are simple and there is no win condition, this was mostly just me trying to wrap my head around reactive extensions in a week. It's not a style I usually code in, but I can see it working for puzzle games like this.

The gameplay loop consists of three different operations acting on the board state: Swap, clear and shift. Swap switches the flavors of two adjacent donuts on the board. Clear deactivates cells (grouped into "lines"). Shift replaces cleared cells by shifting all the donuts above down and creating new ones at the top. Operations implement the interface **IOperation**. This way they can be collected into a single **IObservable** sequence. The operation classes only contain the data relevant to the operation, and the actual functionality exists in **GameController**. The sequence of operations gets applied to the game state and then transformed into a sequence of animations. The animations play one by one ensuring the visuals and game state don't get out of sync.

A sequence of player inputs gets transformed into a sequence of swap operations. The other two operations originate from a sequence of game updates where they get fired if certain conditions are met (If there are empty spaces, shift and fill them, if there are clearable lines of donuts, clear them). These sequences are then merged to form the final sequence of operations.

## Gameplay video

https://github.com/user-attachments/assets/1502bc72-983b-4bb4-993a-542e137312dd


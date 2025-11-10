# deGremlin
##### A tool to decrypt and patch strings obfuscated with Appfuscator. Tested on Gremlin Stealer.
<img src="images/clown.png" alt="Clown" width="400" height="300">



# USAGE

````

       __          ______                              __    _
      |  ]       .' ___  |                            [  |  (_)
  .--.| | .---. / .'   \_| _ .--.  .---.  _ .--..--.   | |  __   _ .--.
/ /'`\' |/ /__\\| |   ____[ `/'`\]/ /__\\[ `.-. .-. |  | | [  | [ `.-. |
| \__/  || \__.,\ `.___]  || |    | \__., | | | | | |  | |  | |  | | | |
 '.__.;__]'.__.' `._____.'[___]    '.__.'[___||__||__][___][___][___||__]
                                                                        P.S: little bit Appfuscator

Usage:
degremlin.exe [filepath] [method_token_in_hex]
````


# Example
##### This is from the Gremlin Stealer sample ( https://bazaar.abuse.ch/sample/d21c8a005125a27c49343e7b5b612fc51160b6ae9eefa0a0620f67fa4d0a30f6/ )

![](images/before_after.png)


# IT DOES
- [x] Patch most obfuscated strings
- [x] Simplify Addition, Subtraction, Multiplication and XOR mixed boolean arithmetic
- [x] Eliminate sizeof's
- [x] Eliminate EmptyType

# TO-DO
- [ ] Patch terneary operator
- [ ] Replace variables by their values
- [ ] Cover more patterns



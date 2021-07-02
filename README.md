OPC2WEB client is a program that connects to the OPC DA server specified in the config (opc2web-client.exe.config), collects the necessary tags (those specified in the tags.txt file), and returns their values ​​taking into account the coefficient and offset as a JSON string by the port specified in the config. That is, by opening the page 127.0.0.1:45455 in the browser (45455 is the port number from the config), you can see the values ​​of all tags from the tags.txt file. In order for the display of tags in the browser to be readable, you need to create a web page using AJAX that will pick up tags from the local host and display them in the browser. For example, like here https://github.com/boolkin/opc2web

The tags.txt file format is 5 columns separated by tabs:

No. Tag in OPC Coefficient Offset Boolean type


Accordingly, tags are added in the same way:

0 [R04_15294_Z]zzz_test 0.5 0 b


1 column - number in order from zero (0)

2 column - tag name, as it is seen by OPC clients ([R04_15294_Z]zzz_test)

3 column - coefficient or Gain by which the number is multiplied. The fractional part is separated by a comma, not a period. Put a minus in front of the digit if negative for example -5 without a space (0.5)

4 column - offset or Offset which is added to the number. Both positive and negative offset (0) can be used

5th column - boolean type sign - b - means bool (1 = true, 0 = false)!b - the same but with reverse logic (0 = true, 1 = false), the rest of the characters are ignored, but for convenience you can put s - single format.

All columns are required, otherwise you will not be able to parse the data.

After adding all the necessary lines with tags, save the file and restart the opc2web service, the new tags will automatically be pulled up.

When text files with errors appear in the program folder, you can open it and see what could lead to the error: at the moment, only 2 exceptions are being processed - incorrect coefficient or offset format (a point instead of a comma or any unknown character, including a space) , as well as the wrong number of columns per line or not separated by tabs, there may be an extra line at the end


To view the list of tags, you can use any OPC client, for example, download it for free from here https://www.kassl.de/opc/download.shtml

You can check the client's work using the OPC server simulator, for example, GrayBox http://gray-box.net/download_graysim.php?lang=ru

To access tags from any computer on the network, you need to install an HTTP server on the computer where this client is running, for example, nginx https://nginx.org/ru/download.html
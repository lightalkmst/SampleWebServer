// Script initialization
// I'm using functions for their scoping properties
// Although this is a sample project, this allows for future augmentation without worrying as much about namespaces
var scope = {};
scope.vars = {};

(scope.load = function() {
	// Query for the json file from the server
	$(document).ready(function() {
		$.getJSON('Colors.json', function(json) {
			scope.vars.colors = json.colors;
			scope.draw();
		});
	});

	// Construct the button list
	scope.draw = function(color) {
		var buttons = '';
		for (var i = 0; i < scope.vars.colors.length; i++) {
			// Sets the header to display the chosen color and not create a button for it
			if (scope.vars.colors[i] === color) {
				document.getElementById('header').innerHTML = 'My favorite color is <font color="' + color + '">' + color + '</font>';
				continue;
			}

			// HTML for the individual buttons
			// They will call scope.chooseColor() with their color on click
			buttons += '<button type="button" onclick="scope.chooseColor(scope.vars.colors[' + i + '])">' + scope.vars.colors[i] + '</button><br />';
		}
		document.getElementById('buttonDiv').innerHTML = buttons;
	};

	// Handle button clicking
	scope.chooseColor = function(color) {
		// Sets the current favorite color
		scope.vars.color = color;

		// Redraw the button list and header
		scope.draw(color);
	};
})();
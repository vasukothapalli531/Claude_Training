// Fixture: dangerous JS calls.
function run(input) {
  eval(input);
  setTimeout("doStuff()", 100);
  const f = new Function("return 1");
}

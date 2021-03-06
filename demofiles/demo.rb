#####################
#Interop
#####################
dir = "C:\\Users\\jdeville\\projects\\irwitty\\demofiles"
Dir.chdir(dir)
spelling = IronRuby.require 'spelling'
spelling.correct.call('speling'.to_clr_string)
##############
#Hosting
##############
require 'IronPython'
dir = "C:\\Users\\jdeville\\projects\\irwitty\\demofiles"
e = nil
Dir.chdir(dir) do
  e = IronPython::Hosting::Python.create_engine
  $s = e.create_scope
  e.execute 'from spelling import *', $s
end
class << e
  def correct(str)
    execute("correct('#{str}')", $s).to_s
  end
end

e.correct "speling"
